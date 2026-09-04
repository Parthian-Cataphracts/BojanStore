using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Bojan.Api;
using Bojan.Api.Auth;
using Bojan.Api.Endpoints;
using Bojan.Api.Knight;
using Knight.StoreAgent;
using Bojan.Application.Common;
using Bojan.Application.Notifications;
using Bojan.Infrastructure;
using Bojan.Infrastructure.Auth;
using Bojan.Infrastructure.Persistence;
using Bojan.Infrastructure.Persistence.Seed;
using Bojan.Infrastructure.Storage;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Console and file, not console alone.
//
// Everything this application has ever said about itself went to stdout and
// nowhere else, which means the only way to read why a page returned 500 was to
// reach the host and run `docker logs`. That is a reasonable thing to ask of an
// engineer and an unreasonable thing to ask of the person whose shop it is, and
// it is why a fault that hit every category page could sit there being reported
// by customers rather than read off a screen.
//
// The file the panel reads is this one. Rolled daily and capped, because a log
// nobody prunes is a disk that fills: fourteen files, 32MB each, so the worst
// case is bounded at half a gigabyte and the oldest thing readable is a
// fortnight old. `shared` because the archiver and the app can both have it
// open, and the flush interval is short so a line is on disk by the time
// somebody goes looking for it.
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(
            context.Configuration["Logs:Directory"] is { Length: > 0 } configured
                ? configured
                // The reader's own default, not a second spelling of it: these
                // are one directory, and when they disagreed the panel reported
                // an empty log on a host that was writing one.
                : Bojan.Infrastructure.Diagnostics.LogFileOptions.DefaultDirectory,
            "bojan-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        fileSizeLimitBytes: 32L * 1024 * 1024,
        rollOnFileSizeLimit: true,
        shared: true,
        flushToDiskInterval: TimeSpan.FromSeconds(2)));

builder.Services.AddInfrastructure(builder.Configuration);

/*
    Refuse to boot with the development email host on a real deployment.

    `EmailLinks.Site` defaults to http://localhost:3000 so a developer's machine
    needs no configuration, and nothing forced a deployment to replace it. Unset,
    every link the shop emails — the address-verification link, the password
    reset, the link to an order — pointed at the *recipient's* own machine. They
    click it, get a connection error, and the address they were verifying stays
    unverified. Nothing fails on the shop's side, so nothing is logged and
    nothing looks wrong from the panel: the only symptom is customers who cannot
    finish signing up and do not say why.

    Checked here rather than beside the binding in AddInfrastructure, because
    only here is the environment authoritative. That check read
    `ASPNETCORE_ENVIRONMENT` out of configuration, which the test host does not
    set — `WebApplicationFactory.UseEnvironment` sets the host's environment
    without writing that key — so the guard fired against every test instead of
    against the deployment it is for.
*/
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddOptions<EmailLinks>()
        .Validate(
            links => !links.Site.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                && !links.Site.Contains("127.0.0.1", StringComparison.Ordinal),
            "Email:Site still points at localhost, so every link this shop emails would point at " +
            "the recipient's own machine. Set it to the storefront's public address — the " +
            "Email__Site environment variable, which docker-compose.yml fills from " +
            "PUBLIC_STOREFRONT_URL.")
        .ValidateOnStart();
}

// The one line that can give a phone number a fixed sign-in code, guarded by
// the one condition that matters. Everything it enables lives behind
// Auth:DevOtp in appsettings.Development.json, which no other environment
// loads — so on a production host this call does not happen and that
// configuration is never read. See AddDevelopmentSignIn.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDevelopmentSignIn(builder.Configuration);
}

builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// Nulls are omitted rather than written, so an optional DTO field arrives as
// `undefined` — which is what the frontend's TypeScript declares (`field?:`)
// and what its truthiness checks are written against. camelCase is the default
// for minimal APIs and is what every property name in types.ts expects.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

    // One choke point for every instant a request carries, because the
    // alternative is each writer remembering — and the one that did not
    // remember answered "this already exists" to a report nobody had asked for
    // before. See UtcDateTimeOffsetConverter.
    options.SerializerOptions.Converters.Add(new Bojan.Api.Contracts.UtcDateTimeOffsetConverter());
});

/*
    A ceiling on how large a request body may be.

    Kestrel's default is thirty megabytes, and nothing here needs anywhere near
    that: the JSON bodies are kilobytes, and the largest legitimate request is an
    image upload, which the upload endpoint itself caps at eight. Thirty is an
    invitation to spend this process's memory and the proxy's bandwidth on a
    body that will be rejected after it has all arrived.

    Twelve rather than eight — the multipart envelope, its boundaries and the
    field names all count toward this, and a limit that cut off a file exactly
    at the size the upload screen says it accepts would refuse the last few
    kilobytes of a legal upload.
*/
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 12L * 1024 * 1024);

builder.Services.AddProblemDetails();

// A write the database refuses is a conflict with the current state, not a
// server fault. Without this every unique-index violation reached the client as
// a 500 — see PersistenceConflictHandler.
builder.Services.AddExceptionHandler<PersistenceConflictHandler>();

builder.Services.AddApiRateLimiting(builder.Configuration);

// Who the caller is, when something stands in front of this process.
//
// The rate limiters bucket by RemoteIpAddress, so that address has to be the
// real one — and behind a proxy it is the proxy's until this middleware
// rewrites it from X-Forwarded-For. It does that from the trusted end of the
// chain, which is the part the caller cannot forge; the limiters used to read
// the left-most entry themselves, which is the part the caller writes.
//
// ForwardLimit is how many proxies stand in front. One matches the shipped
// topology — every container published on loopback with a single reverse proxy
// ahead of it. A CDN in front of that proxy makes it two.
//
// Which peers may claim to be forwarding. Clearing both lists, which is the
// usual shorthand, means "trust every peer" — and a header trusted from every
// peer is the bypass again by another route. The peer here is either loopback
// (the reverse proxy on the host, since every container publishes on 127.0.0.1)
// or a private container address, so those are what is trusted and nothing else.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = builder.Configuration.GetValue<int?>("Network:TrustedProxyHops") ?? 1;

    // KnownIPNetworks and System.Net.IPNetwork, not the HttpOverrides pair of
    // the same names: those are obsolete as of this framework version and warn
    // on every build. Same semantics, so the trust list is unchanged.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();

    foreach (var (prefix, length) in TrustedProxyNetworks)
    {
        options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse(prefix), length));
    }
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Two ways in, both producing the same claims — see
// TrustedProxyAuthenticationHandler for why both exist and which one the
// shipped frontend uses today.
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Read inside this delegate, not into a local above it.
        //
        // The delegate runs when the options are first resolved, which is after
        // the host is built and every configuration source is in place; a line
        // above it runs while the builder is still being assembled. The key used
        // to be hoisted while the issuer and audience below were not, so any
        // source registered after this point — which is exactly what a test host
        // does — gave an API that signed with the real key and validated against
        // the placeholder. Every bearer request answered 401 "signature key was
        // not found", while the trusted-proxy scheme kept working and hid it.
        var signingKey = jwtSection["SigningKey"];

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                // A missing or short signing key does not fail here —
                // Infrastructure's JwtOptions registration (.ValidateOnStart())
                // is what refuses to start the app over that, with a message
                // pointing at Jwt__SigningKey. Falling back to a placeholder
                // only keeps *this* registration from throwing before that
                // clearer error gets the chance to.
                Encoding.UTF8.GetBytes(string.IsNullOrEmpty(signingKey) ? new string('0', 32) : signingKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    })
    .AddScheme<TrustedProxyOptions, TrustedProxyAuthenticationHandler>(
        TrustedProxyOptions.SchemeName,
        options => builder.Configuration.GetSection(TrustedProxyOptions.SectionName).Bind(options));

builder.Services.AddApiAuthorization();

/*
    The shop's connection to KNIGHT.

    The agent is a vendored library and it does nothing until this shop has a
    credential — which an owner enters on a settings screen rather than an
    engineer setting an environment variable, because the person who owns the
    shop is rarely the person who can restart it.

    What this line adds is a background poller, a settings surface and, once
    something has been delivered, routes under /api/features. Nothing else in
    the application knows the platform exists.
*/
builder.Services.AddKnightIntegration(builder.Configuration);

// Screen 157 — system status. Checks the database because that is the one
// dependency whose failure the panel actually needs to know about; more checks
// (SMS gateway, storage) join this as they are built.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BojanDbContext>("database")
    // The board had one row on it. A shop whose mail is not configured sends
    // nothing and says nothing about it — see OutboundMailHealthCheck.
    .AddCheck<Bojan.Api.Diagnostics.OutboundMailHealthCheck>("email");

// KNIGHT, the control plane this shop takes its optional Features from.
//
// Two background services: one tells KNIGHT what this store runs, the other
// asks it for work and installs whatever it hands over. Both are outbound only
// — KNIGHT never connects inward — so this needs no inbound port and no change
// to how the shop is deployed.
//
// Off unless `Knight:Enabled` is true and a credential is configured, so a
// developer machine and the test host do nothing. See the store agent's README
// in Parthian-Cataphracts/Knight for what to set.
builder.Services.AddKnightStoreAgent(builder.Configuration);

var app = builder.Build();

// Before anything that reads the caller's address — the rate limiter below is
// the reason this is here at all.
app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseStatusCodePages();

// A request the framework could not even bind is the caller's fault, not this
// server's.
//
// `?page=abc` against any endpoint with a typed query parameter throws
// BadHttpRequestException, which carries its own 400 — and UseExceptionHandler
// treats every exception alike, so all of them came back 500. Every paged list
// in the panel answered a typo with a server error, and anything watching 5xx
// counted it as an outage.
//
// *After* UseExceptionHandler, which means inside it: middleware registered
// earlier wraps what follows, so a handler that catches this would have to sit
// outside the one that turns it into a 500 — and by then the response has
// already been written. Being inner also means anything this does not catch
// still reaches the handler and the log untouched.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (BadHttpRequestException exception)
    {
        // Nothing about which parameter failed: the caller sent it, so they can
        // see it, and repeating it back is how a reflected value ends up in a
        // log or an error page that renders it.
        await Results
            .Problem(title: "malformed-request", statusCode: exception.StatusCode)
            .ExecuteAsync(context);
    }
});

/*
    One line per request, and enough of it to answer "who did this".

    The default template records the method, the path, the status and how long
    it took — which says what happened and nothing about whom it happened for.
    The enrichment below adds the caller: the operator id for a panel request,
    the customer id for a storefront one, and the address the request came from.
    That is what turns the log from a performance trace into a record of
    activity, and it is what somebody reading it after the fact is actually
    looking for.

    The completion callback runs after the pipeline has unwound, so
    authentication has already resolved the principal by the time this reads it
    — which is why the placement of this call ahead of UseAuthentication does
    not matter here, though it looks as though it should.

    No bodies, no query strings, no headers. A request body on this API carries
    passwords, tokens and card-adjacent references, and a log is the one place
    they must never end up — the whole point of storing a hash is undone by
    printing what was hashed.
*/
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnostic, context) =>
    {
        var user = context.User;

        // Always set, never conditionally. Serilog leaves a placeholder it has
        // no property for as the literal text — so the first run of this wrote
        // "by {Scope}/{ActorId}" on every anonymous request, which is most of
        // them on a storefront. A shopper who is not signed in is still an
        // actor worth naming; "anonymous" is the name.
        diagnostic.Set("Scope", user.FindFirstValue("scope") ?? "anonymous");
        diagnostic.Set("ActorId", user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "-");

        if (user.FindFirstValue(ClaimTypes.Role) is { Length: > 0 } role)
        {
            // Not in the template, so no placeholder to strand — it rides along
            // as a structured property for anything querying the file.
            diagnostic.Set("ActorRole", role);
        }

        // Behind UseForwardedHeaders, so this is the real client rather than
        // the reverse proxy — the same address the rate limiter partitions on.
        //
        // Unconditional, for the reason Scope and ActorId are: this one was
        // written inside an `if` and left the literal "{Ip}" at the end of
        // every line whose connection had no address to report — a request over
        // a socket, and every request in the in-process test host. "unknown" is
        // the name the rate limiter's own partition gives that case, so a line
        // that says it and a window that buckets it agree.
        diagnostic.Set("Ip", context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    };

    options.MessageTemplate =
        "{RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.0}ms by {Scope}/{ActorId} from {Ip}";
});

/*
    There is no CORS policy here, and that is the design rather than an
    omission.

    No browser is supposed to reach this API. Both sites talk to it from their
    own Node process — `API_BASE_URL` in the compose file points at
    `http://api:8080`, a name that resolves only inside the compose network —
    and every one of those calls carries `X-Api-Key`, which is what the trusted
    proxy scheme checks. A page that fetched this origin directly would be
    handing that key to a browser to leak.

    The only thing a browser does load straight from here is `/media`, and
    images in `<img>` and `background-image` are not subject to the same-origin
    read that CORS exists to relax.

    So the absence of `AddCors` is what keeps a cross-origin script from calling
    the API at all, and adding a permissive policy would remove a boundary
    rather than fix a bug. If a client that *is* a browser is ever built against
    this — a mobile web app, a separate front end — the policy it needs is a
    named one listing that origin, added here, and it must be paired with a
    story for the API key, because an origin allow-list alone would then be the
    only thing standing in front of it.
*/

app.UseUploadedMedia();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

/*
    Uploaded media is mounted once, by `UseUploadedMedia()` above.

    There were two registrations of the same directory, and they did not agree
    about where it is: this one resolved `RootPath` against `ContentRootPath`
    while the middleware resolves it against the process's working directory.
    Identical whenever the path is absolute — which it is in the shipped compose
    file — and two different directories the moment it is not, with the second
    mount shadowing the first and caching differently. One mount, and the one
    that also sends `nosniff` and an inline disposition for files strangers
    uploaded.
*/

/*
    Where a delivered Feature is served from.

    After authentication, because the store has to know who is asking before it
    can assert it to somebody else's service, and inside a branch of its own so
    a delivered configuration cannot shadow a route this shop already serves.
    Middleware rather than routes, because a Feature arrives while the shop is
    running and a route table built at start-up would make every install need a
    redeploy first.
*/
app.Map(KnightIntegration.ProxyBasePath, branch => branch.UseKnightFeatureProxy());

// Health sits outside /api: it is for the panel and for whatever watches the
// process, not for the storefront's data layer.
app.MapHealthChecks("/health");

// Two base paths, matching the two apps' .env.example files exactly: the
// storefront calls /api, the panel calls /api/admin. A resource with the same
// name under each is a *different* endpoint — BACKEND.md section 0.
var api = app.MapGroup("/api");
api.MapAuthEndpoints();
api.MapCatalogueEndpoints();
api.MapAccountEndpoints();
api.MapAccountVerificationEndpoints();
api.MapCheckoutEndpoints();
api.MapPublicWriteEndpoints();
api.MapUploadEndpoints();
api.MapStoreStatusEndpoints();
api.MapLiveChatEndpoints();
api.MapPushEndpoints();

var admin = api.MapGroup("/admin");
admin.MapAdminAuthEndpoints();
admin.MapAdminReadEndpoints();
admin.MapAdminWriteEndpoints();
admin.MapMailboxEndpoints();
admin.MapPaymentSettingsEndpoints();
admin.MapSmsSettingsEndpoints();
admin.MapVerificationSettingsEndpoints();
admin.MapShippingSettingsEndpoints();
admin.MapLoyaltyEndpoints();
admin.MapPushSettingsEndpoints();
admin.MapKnightEndpoints();

await MigrateIfRequestedAsync(app);
await SeedIfRequestedAsync(app);

app.Run();

/// <summary>
/// Brings the database schema up to date before anything reads it.
/// </summary>
/// <remarks>
/// Off by default: a developer with a database owns its schema, and applying
/// migrations behind their back on every <c>dotnet run</c> is not welcome. It
/// exists for the container image, whose compose file turns it on — there the
/// database starts empty on first boot, and without this the seeder below is
/// the first thing to touch a schema that does not exist yet. Migrating is
/// idempotent, so a restart against an up-to-date database does nothing.
///
/// Single-instance deployments only. Several API replicas starting at once
/// would each try to take the migration lock; that wants a separate migration
/// step in the deployment, not this.
/// </remarks>
static async Task MigrateIfRequestedAsync(WebApplication app)
{
    if (!app.Configuration.GetValue<bool>("Database:AutoMigrate"))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();

    app.Logger.LogInformation("Applying database migrations.");
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database schema is up to date.");
}

/// <summary>
/// Runs the catalogue seeder when configuration asks for it.
/// </summary>
/// <remarks>
/// Gated on <c>Seed:Enabled</c> rather than on the environment, so a staging
/// box can be filled deliberately and a production one cannot be filled by
/// accident. The seeder skips every table that already has rows, so leaving the
/// flag on is idempotent rather than destructive.
/// </remarks>
static async Task SeedIfRequestedAsync(WebApplication app)
{
    if (!app.Configuration.GetValue<bool>("Seed:Enabled"))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<CatalogueSeeder>();

    await seeder.SeedAsync(
        app.Configuration["Seed:AdminPassword"],
        // The number the owner signs in with, on both sides. See the seeder.
        app.Configuration["Seed:AdminPhone"],
        // The account the fixed sign-in code signs into. Only ever non-null in
        // Development, for the same reason the code itself is.
        app.Environment.IsDevelopment() ? app.Configuration["Auth:DevOtp:Phone"] : null);
}

/// <summary>Exposed so <c>Bojan.Api.Tests</c> can host the app in-memory via <c>WebApplicationFactory</c>.</summary>
public partial class Program
{
    /// <summary>
    /// The only peers whose forwarding header is believed: loopback, and the
    /// private ranges a container network is drawn from.
    /// </summary>
    /// <remarks>
    /// Deliberately not the whole internet. A caller reaching this from
    /// anywhere else has its <c>X-Forwarded-For</c> ignored and is rate-limited
    /// on the address it actually connected from.
    /// </remarks>
    private static readonly (string Prefix, int Length)[] TrustedProxyNetworks =
    [
        ("127.0.0.0", 8),
        ("::1", 128),
        ("10.0.0.0", 8),
        ("172.16.0.0", 12),
        ("192.168.0.0", 16),
    ];
}
