using Bojan.Application.Auth;
using Bojan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bojan.Api.Tests;

/// <summary>
/// Hosts the real API in-process against SQLite in-memory instead of
/// PostgreSQL.
/// </summary>
/// <remarks>
/// This proves the endpoint wiring, validation, DI graph, EF model and the
/// auth flow end to end with a real HTTP client — everything except
/// Postgres-specific SQL. Swapping the provider is the trade-off that makes
/// that possible without a Docker daemon on the box running the tests: the
/// SQLite connection is kept open for the factory's lifetime so the
/// in-memory database survives between requests, and the schema is created
/// directly from the model rather than by replaying migrations (SQLite does
/// not need the migration history to prove the model is sound; the
/// Postgres-specific migration SQL is checked separately via
/// <c>dotnet ef migrations script</c>, not by this factory).
/// </remarks>
public class BojanApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>The shared secret the tests present as <c>X-Api-Key</c>.</summary>
    public const string TrustedProxyKey = "test-trusted-proxy-key";

    /// <summary>Where a test reads back the code an OTP request generated — see <see cref="CapturingSmsSender"/>.</summary>
    public CapturingSmsSender Sms { get; } = new();

    /// <summary>Where a test reads back the token a password-reset request emailed.</summary>
    public CapturingEmailSender Email { get; } = new();

    /// <summary>
    /// A client that authenticates as one customer, the way the storefront's
    /// write proxy does.
    /// </summary>
    /// <remarks>
    /// The security stamp is read from the account rather than passed in, so a
    /// test that only knows a customer id still builds a client the API accepts.
    /// Sending it is not optional: a customer request without one is refused,
    /// which is what makes a password reset able to end a session.
    /// </remarks>
    public HttpClient CreateCustomerClient(Guid customerId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();
        var stamp = db.Customers.Where(c => c.Id == customerId).Select(c => c.SecurityStamp).First();

        return CreateCustomerClient(customerId, stamp);
    }

    /// <summary>A client stamped explicitly — for the tests that are about the stamp itself.</summary>
    public HttpClient CreateCustomerClient(Guid customerId, Guid securityStamp)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", TrustedProxyKey);
        client.DefaultRequestHeaders.Add("X-Customer-Id", customerId.ToString());
        client.DefaultRequestHeaders.Add("X-Customer-Stamp", securityStamp.ToString());
        return client;
    }

    /// <summary>
    /// A client that authenticates as one operator, the way the panel's write
    /// route does.
    /// </summary>
    /// <remarks>
    /// The stamp is read from the account for the same reason the customer
    /// client's is, and is just as non-optional: an operator request without one
    /// is refused, which is what makes a password change end the sessions
    /// someone else is holding.
    /// </remarks>
    public HttpClient CreateAdminClient(Guid adminId, bool allowAutoRedirect = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();
        var stamp = db.AdminUsers.Where(a => a.Id == adminId).Select(a => a.SecurityStamp).FirstOrDefault();

        return CreateAdminClient(adminId, stamp, allowAutoRedirect);
    }

    /// <summary>A client stamped explicitly — for the tests that are about the stamp itself.</summary>
    public HttpClient CreateAdminClient(Guid adminId, Guid securityStamp, bool allowAutoRedirect = true)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });
        client.DefaultRequestHeaders.Add("X-Api-Key", TrustedProxyKey);
        client.DefaultRequestHeaders.Add("X-Admin-User", adminId.ToString());
        client.DefaultRequestHeaders.Add("X-Admin-Stamp", securityStamp.ToString());
        return client;
    }

    /// <summary>Runs work against the same in-memory database the host is using.</summary>
    public async Task WithDbAsync(Func<BojanDbContext, Task> work)
    {
        using var scope = Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<BojanDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Enough for JwtOptions' own startup validation to pass; the
            // signing key format is not otherwise special.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "bojan-api-tests",
                ["Jwt:Audience"] = "bojan-tests",
                ["Jwt:SigningKey"] = "test-signing-key-at-least-32-characters-long",

                // Lets the tests authenticate the way the shipped frontend
                // does — X-Api-Key plus an identity header. See
                // TrustedProxyAuthenticationHandler for why that scheme exists.
                ["TrustedProxy:ApiKey"] = TrustedProxyKey,

                // The seeder is a startup step, and these tests build their own
                // fixtures per case; seeding 33 products into every one would
                // make each test's arrangement harder to read, not easier.
                ["Seed:Enabled"] = "false",

                // Every test in a class shares one host, so they also share a
                // rate-limit partition — the whole class comes from the same
                // loopback address. The shipped ceiling of eight sign-ins per
                // five minutes is the tightest, and a class with more sign-ins
                // than that would fail on the limiter rather than on what it
                // set out to check. RateLimitingTests overrides this back down
                // to prove the limiter itself.
                ["RateLimits:AdminLogin:PermitLimit"] = "1000",
                ["RateLimits:AdminLogin:WindowSeconds"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            // AddDbContext registers more than the DbContextOptions<T>
            // descriptor itself — modern EF Core also adds an
            // IDbContextOptionsConfiguration<T> entry per call, and that one
            // is an *enumerable* registration: removing only
            // DbContextOptions<T> leaves Program.cs's original UseNpgsql
            // configuration action sitting alongside this one, and EF then
            // applies both to the same options object ("services for
            // database providers 'Npgsql...', 'Sqlite' have been
            // registered"). Every descriptor that mentions BojanDbContext has
            // to go before re-adding.
            services.RemoveAll(typeof(DbContextOptions<BojanDbContext>));
            services.RemoveAll(typeof(DbContextOptions));
            var configurationServiceType = Type.GetType(
                "Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration`1, Microsoft.EntityFrameworkCore")
                ?.MakeGenericType(typeof(BojanDbContext));
            if (configurationServiceType is not null)
            {
                services.RemoveAll(configurationServiceType);
            }

            services.AddDbContext<BojanDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<ISmsSender>();
            services.AddSingleton<ISmsSender>(Sms);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);

            // The background pollers do not run in tests.
            //
            // They are singletons holding their own scope against the one
            // SQLite connection these tests share, and they keep polling while
            // a test finishes — so a cycle that lands during teardown closes
            // over a connection the factory is disposing, which surfaces as a
            // NullReferenceException from inside SqliteConnection.Close and has
            // nothing to do with the test that appears to have failed.
            //
            // Nothing here drives them: every test exercises the endpoint that
            // queues the work, and the worker's own behaviour is the dispatcher's
            // to prove.
            services.RemoveAll<IHostedService>();
        });
    }

    /// <summary>Creates the schema fresh — call once per test class, before any request is made.</summary>
    public void EnsureDatabaseCreated()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<BojanDbContext>().Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
