using Bojan.Application.Auth;
using Bojan.Application.Common;
using Bojan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bojan.Api.Tests;

/// <summary>
/// Hosts the real API in-process against a real PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// This proves the endpoint wiring, validation, DI graph, EF model and the auth
/// flow end to end with a real HTTP client — and, now, the SQL. It used to run
/// on SQLite in memory, which was the documented trade: "everything except
/// Postgres-specific SQL". What that quietly excluded was every
/// <c>FOR UPDATE</c> in the codebase. Those statements are all written
/// <c>if (db.Database.IsNpgsql())</c>, because SQLite has no row locks and
/// serialises writers itself — so on the old host they did not execute, and the
/// locks that make settling a payment and crediting a wallet idempotent under
/// concurrency were the one part of the money path with no coverage at all.
/// </para>
/// <para>
/// Each factory gets its own database, copied from a template the
/// <see cref="PostgresFixture"/> migrated once. A test class therefore has a
/// real, isolated, fully migrated schema, and the migrations themselves are
/// exercised on every run.
/// </para>
/// </remarks>
public class BojanApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// This class's own database, copied from the migrated template.
    /// </summary>
    /// <remarks>
    /// Taken in the constructor rather than lazily, because the connection
    /// string has to exist before <see cref="ConfigureWebHost"/> runs — and
    /// blocking here is what lets the forty-odd classes that write
    /// <c>new BojanApiFactory()</c> keep writing it.
    /// </remarks>
    private readonly string _connectionString = PostgresServer.CreateDatabaseAsync().GetAwaiter().GetResult();

    private string Database => new Npgsql.NpgsqlConnectionStringBuilder(_connectionString).Database!;

    /// <summary>The shared secret the tests present as <c>X-Api-Key</c>.</summary>
    public const string TrustedProxyKey = "test-trusted-proxy-key";

    /// <summary>Where a test reads back the code an OTP request generated — see <see cref="CapturingSmsSender"/>.</summary>
    public CapturingSmsSender Sms { get; } = new();

    /// <summary>Where a test reads back the token a password-reset request emailed.</summary>
    public CapturingEmailSender Email { get; } = new();

    /// <summary>
    /// Stands in for pg_dump, which has nothing to dump on a SQLite host.
    /// </summary>
    /// <remarks>
    /// Exposed so a test can make the tooling unavailable or make the dump fail
    /// — the two cases the backup worker has to record as failures rather than
    /// paper over.
    /// </remarks>
    public StubDatabaseDumper Dumper { get; } = new();

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

    /// <summary>Runs work against the same database the host is using.</summary>
    public async Task WithDbAsync(Func<BojanDbContext, Task> work)
    {
        using var scope = Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<BojanDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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

            services.AddDbContext<BojanDbContext>(options => options.UseNpgsql(_connectionString));

            services.RemoveAll<ISmsSender>();
            services.AddSingleton<ISmsSender>(Sms);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);

            services.RemoveAll<IDatabaseDumper>();
            services.AddSingleton<IDatabaseDumper>(Dumper);

            // The background pollers do not run in tests.
            //
            // They hold their own scope and keep polling while a test finishes,
            // so a cycle landing during teardown queries a database the factory
            // is dropping — which surfaces as a failure in whichever test
            // happened to be running rather than in the worker.
            //
            // Nothing here drives them: every test exercises the endpoint that
            // queues the work, and the worker's own behaviour is the dispatcher's
            // to prove.
            services.RemoveAll<IHostedService>();
        });
    }

    /// <summary>
    /// Seeds what every test class assumes exists.
    /// </summary>
    /// <remarks>
    /// The schema arrives already migrated, copied from the run's template, so
    /// this no longer creates it — the name is kept because every test class
    /// calls it and what it means to a caller has not changed.
    /// </remarks>
    public void EnsureDatabaseCreated()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();

        // Which gateway the shop uses is a stored setting rather than a
        // registration, so a fresh database has none and every checkout that
        // needs a redirect would answer payment-unavailable. The stub is
        // selected here for the same reason it exists: it makes the money path
        // reachable end to end without a PSP. A test that is about the
        // configuration itself overwrites these rows.
        //
        // Written only when absent, because xUnit builds a new instance of a
        // test class per test method and every one of those constructors calls
        // this. `EnsureCreated` is idempotent; an unconditional insert is not,
        // and the second one hits the unique index on (Section, Key).
        var wanted = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["provider"] = Application.Contracts.PaymentProviders.Sandbox,
            ["callbackUrl"] = "http://localhost:3000/checkout/payment/callback",
        };

        var existing = db.Settings
            .Where(entry => entry.Section == "payment")
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (key, value) in wanted.Where(pair => !existing.Contains(pair.Key)))
        {
            db.Settings.Add(new Domain.Admin.SettingEntry
            {
                Section = "payment",
                Key = key,
                Value = value,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // The host is down, so nothing is holding the database open. Dropped
            // rather than left behind: a suite of several hundred classes would
            // otherwise leave several hundred databases on the server.
            PostgresServer.DropDatabaseAsync(Database).GetAwaiter().GetResult();
        }
    }
}
