using Bojan.Application.Auth;
using Bojan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
public sealed class BojanApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>The shared secret the tests present as <c>X-Api-Key</c>.</summary>
    public const string TrustedProxyKey = "test-trusted-proxy-key";

    /// <summary>Where a test reads back the code an OTP request generated — see <see cref="CapturingSmsSender"/>.</summary>
    public CapturingSmsSender Sms { get; } = new();

    /// <summary>
    /// A client that authenticates as one customer, the way the storefront's
    /// write proxy does.
    /// </summary>
    public HttpClient CreateCustomerClient(Guid customerId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", TrustedProxyKey);
        client.DefaultRequestHeaders.Add("X-Customer-Id", customerId.ToString());
        return client;
    }

    /// <summary>A client that authenticates as one operator, the way the panel's write route does.</summary>
    public HttpClient CreateAdminClient(Guid adminId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", TrustedProxyKey);
        client.DefaultRequestHeaders.Add("X-Admin-User", adminId.ToString());
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
