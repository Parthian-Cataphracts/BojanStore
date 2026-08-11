using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bojan.Api.Tests;

/// <summary>
/// One real PostgreSQL for the whole test run.
/// </summary>
/// <remarks>
/// <para>
/// The suite used to run on SQLite in memory, and the trade was written down at
/// the time: everything except Postgres-specific SQL. What that quietly
/// excluded was every <c>FOR UPDATE</c> in the codebase — the locks that make
/// settling a payment, approving a wallet top-up and placing an order
/// idempotent when two requests arrive together. Each of those statements is
/// guarded by <c>if (db.Database.IsNpgsql())</c>, because SQLite has no row
/// locks and serialises writers itself, so on the old host they never ran. The
/// logic around them was covered; the thing they exist for was not, and this
/// application has exactly one database in production.
/// </para>
/// <para>
/// The schema is built by replaying the real migrations rather than by
/// <c>EnsureCreated</c>, which is the second thing the old host could not do: a
/// migration that does not apply cleanly now fails the suite instead of waiting
/// for a deploy.
/// </para>
/// <para>
/// Migrating once per test class would cost minutes, so it happens once here
/// into a template database and every factory takes a copy with
/// <c>CREATE DATABASE … TEMPLATE …</c> — a file copy inside the server, fast
/// enough that each test class can have a real database of its own rather than
/// a shared one they would all have to tiptoe around.
/// </para>
/// <para>
/// Static rather than an xUnit fixture so that <c>new BojanApiFactory()</c>
/// keeps working in the forty-odd classes that write it that way. The container
/// starts on first use; Testcontainers' own reaper removes it when the run
/// ends, including a run that crashed.
/// </para>
/// </remarks>
internal static class PostgresServer
{
    private const string TemplateDatabase = "bojan_template";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static PostgreSqlContainer? _container;

    /// <summary>Connection string for the maintenance database, where copies are made from.</summary>
    private static string _adminConnectionString = string.Empty;

    private static string _templateConnectionString = string.Empty;

    /// <summary>
    /// A fresh, fully migrated database, and the connection string for it.
    /// </summary>
    public static async Task<string> CreateDatabaseAsync()
    {
        await EnsureStartedAsync();

        var name = $"bojan_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"""CREATE DATABASE "{name}" TEMPLATE "{TemplateDatabase}" """;
        await command.ExecuteNonQueryAsync();

        return Rewrite(_templateConnectionString, name);
    }

    /// <summary>Drops a class's database once its factory is finished with it.</summary>
    public static async Task DropDatabaseAsync(string database)
    {
        if (_container is null) return;

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        // WITH (FORCE) rather than a polite drop: a test that failed part way
        // through may have left a session open, and a leaked database is worse
        // than a terminated backend nobody is using.
        command.CommandText = $"""DROP DATABASE IF EXISTS "{database}" WITH (FORCE)""";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureStartedAsync()
    {
        if (_container is not null) return;

        await Gate.WaitAsync();
        try
        {
            if (_container is not null) return;

            // Pinned to the image the compose file runs, so the suite and
            // production are the same server.
            var container = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase(TemplateDatabase)
                .WithUsername("bojan")
                .WithPassword("bojan-tests")
                .Build();

            await container.StartAsync();

            _templateConnectionString = container.GetConnectionString();
            _adminConnectionString = Rewrite(_templateConnectionString, "postgres");

            var options = new DbContextOptionsBuilder<BojanDbContext>()
                .UseNpgsql(_templateConnectionString)
                .Options;

            await using (var db = new BojanDbContext(options))
            {
                // The real migrations, in order, exactly as a deployment
                // applies them.
                await db.Database.MigrateAsync();
            }

            // Postgres refuses to copy a template while another session is
            // attached, and the pool above is still holding one.
            NpgsqlConnection.ClearAllPools();

            _container = container;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string Rewrite(string connectionString, string database) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = database,
            // Every test class opens its own pool against its own database, and
            // a few dozen of those at the default size is more connections than
            // the server allows. Small pools, and no waiting forever for one
            // that is not coming.
            MaxPoolSize = 6,
            Timeout = 30,
        }.ConnectionString;
}
