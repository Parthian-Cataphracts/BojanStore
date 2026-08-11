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

    /// <summary>
    /// How many connections the server will accept at once.
    /// </summary>
    /// <remarks>
    /// Well clear of what the suite needs — a few dozen live factories at
    /// <see cref="PoolSize"/> each — because the cost of a generous ceiling is a
    /// little memory on a container that lives for one test run, and the cost of
    /// a tight one is a suite that slows to a crawl and then dies.
    /// </remarks>
    private const int MaxConnections = 400;

    /// <summary>
    /// Connections one test class may hold.
    /// </summary>
    /// <remarks>
    /// Small on purpose. A test class talks to its database from one request at
    /// a time almost always, and the few that exercise concurrency need a
    /// handful — so this is sized for those rather than for the default of a
    /// hundred, which would put one class's idle pool over the whole server's
    /// budget.
    /// </remarks>
    private const int PoolSize = 6;

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static PostgreSqlContainer? _container;

    /// <summary>Connection string for the maintenance database, where copies are made from.</summary>
    private static string _adminConnectionString = string.Empty;

    private static string _templateConnectionString = string.Empty;

    /// <summary>
    /// A fresh, fully migrated database, and the connection string for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Synchronous all the way down, and that is the whole point of it. A test
    /// factory needs its connection string before the host is built, so this is
    /// called from a field initializer — and the version that returned a Task
    /// and was unwrapped with <c>GetAwaiter().GetResult()</c> blocked a thread
    /// pool thread on I/O, once per test method, seventeen at a time.
    /// </para>
    /// <para>
    /// The pool answers that by injecting threads at roughly one a second, so
    /// every continuation in the process queues behind the starvation: the
    /// symptom was a suite where the machine sat at eight percent CPU, an
    /// endpoint test that asserts a 404 took three and a half minutes, and
    /// enough requests passed <c>HttpClient</c>'s hundred-second timeout to take
    /// the test host down with them. Nothing was slow. Everything was waiting
    /// for a thread.
    /// </para>
    /// <para>
    /// Npgsql's synchronous API does the same work without a Task to wait on, so
    /// the calling thread does the I/O itself and nothing is borrowed from the
    /// pool. The one unavoidable await is starting the container, which happens
    /// once for the whole run.
    /// </para>
    /// </remarks>
    public static string CreateDatabase()
    {
        EnsureStarted();

        var name = $"bojan_{Guid.NewGuid():N}";

        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"""CREATE DATABASE "{name}" TEMPLATE "{TemplateDatabase}" """;
        command.ExecuteNonQuery();

        return Rewrite(_templateConnectionString, name);
    }

    /// <summary>Drops a class's database once its factory is finished with it.</summary>
    public static void DropDatabase(string database)
    {
        if (_container is null) return;

        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        // WITH (FORCE) rather than a polite drop: a test that failed part way
        // through may have left a session open, and a leaked database is worse
        // than a terminated backend nobody is using.
        command.CommandText = $"""DROP DATABASE IF EXISTS "{database}" WITH (FORCE)""";
        command.ExecuteNonQuery();
    }

    private static void EnsureStarted()
    {
        if (_container is not null) return;

        Gate.Wait();
        try
        {
            if (_container is not null) return;

            // Pinned to the image the compose file runs, so the suite and
            // production are the same server.
            var container = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase(TemplateDatabase)
                .WithUsername("bojan")
                .WithPassword("bojan-tests")
                // xUnit runs test collections in parallel, and this suite gives
                // every test *method* its own factory and its own database — so
                // a couple of dozen pools are open at once, and the server's
                // default ceiling of 100 is not enough for them. Exceeding it
                // does not fail cleanly: a connection request waits out the
                // timeout below instead, so the symptom is a suite that takes
                // twice as long and occasionally kills its own host rather than
                // one that says it ran out of connections.
                .WithCommand("-c", $"max_connections={MaxConnections}")
                .Build();

            // The one blocking wait in the file, and it happens once for the
            // whole run rather than once per test — a single thread parked for
            // a few seconds while a container boots is not what starves a pool.
            container.StartAsync().GetAwaiter().GetResult();

            _templateConnectionString = container.GetConnectionString();
            _adminConnectionString = Rewrite(_templateConnectionString, "postgres");

            var options = new DbContextOptionsBuilder<BojanDbContext>()
                .UseNpgsql(_templateConnectionString)
                .Options;

            using (var db = new BojanDbContext(options))
            {
                // The real migrations, in order, exactly as a deployment
                // applies them.
                db.Database.Migrate();
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
            // any server would allow. Small pools, and no waiting forever for
            // one that is not coming — see MaxConnections for the other half of
            // this arrangement.
            MaxPoolSize = PoolSize,
            Timeout = 30,
        }.ConnectionString;
}
