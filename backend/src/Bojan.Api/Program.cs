using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Bojan.Api.Auth;
using Bojan.Api.Endpoints;
using Bojan.Application.Common;
using Bojan.Infrastructure;
using Bojan.Infrastructure.Auth;
using Bojan.Infrastructure.Persistence;
using Bojan.Infrastructure.Persistence.Seed;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

builder.Services.AddInfrastructure(builder.Configuration);

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
});

builder.Services.AddProblemDetails();
builder.Services.AddApiRateLimiting();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Two ways in, both producing the same claims — see
// TrustedProxyAuthenticationHandler for why both exist and which one the
// shipped frontend uses today.
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var signingKey = jwtSection["SigningKey"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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

// Screen 157 — system status. Checks the database because that is the one
// dependency whose failure the panel actually needs to know about; more checks
// (SMS gateway, storage) join this as they are built.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BojanDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSerilogRequestLogging();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

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
api.MapCheckoutEndpoints();
api.MapPublicWriteEndpoints();
api.MapUploadEndpoints();

var admin = api.MapGroup("/admin");
admin.MapAdminAuthEndpoints();
admin.MapAdminReadEndpoints();
admin.MapAdminWriteEndpoints();

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
        // The account the fixed sign-in code signs into. Only ever non-null in
        // Development, for the same reason the code itself is.
        app.Environment.IsDevelopment() ? app.Configuration["Auth:DevOtp:Phone"] : null);
}

/// <summary>Exposed so <c>Bojan.Api.Tests</c> can host the app in-memory via <c>WebApplicationFactory</c>.</summary>
public partial class Program;
