using System.Diagnostics;
using System.Text;
using Bojan.Application.Common;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bojan.Infrastructure.Storage;

/// <summary>
/// Dumps PostgreSQL by running <c>pg_dump</c> against the same database this
/// process is connected to.
/// </summary>
/// <remarks>
/// <para>
/// Shelling out rather than reading the schema through EF: a dump has to be
/// restorable, which means sequences, indexes, constraints, extensions and
/// ownership, and reimplementing that from the model is how a backup ends up
/// restoring something subtly different from what was backed up. <c>pg_dump</c>
/// is the tool PostgreSQL ships for exactly this and it is what an operator
/// would reach for by hand.
/// </para>
/// <para>
/// The password goes in the child process's environment, never on its command
/// line — an argument list is readable by every other process on the host.
/// </para>
/// <para>
/// Output is the custom format (<c>-Fc</c>), not plain SQL: it is compressed,
/// and <c>pg_restore</c> can read parts of it selectively, which matters when
/// what is needed back is one table rather than the whole shop.
/// </para>
/// </remarks>
public sealed class PgDumpRunner(string connectionString, ILogger<PgDumpRunner> logger) : IDatabaseDumper
{
    /// <summary>
    /// How long a dump may run before it is abandoned.
    /// </summary>
    /// <remarks>
    /// Long enough for a shop-sized database on slow disks, short enough that a
    /// wedged child process is eventually noticed rather than held open until
    /// the container is restarted.
    /// </remarks>
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var probe = Start("--version", redirectStdout: true);
            if (probe is null) return false;

            await probe.WaitForExitAsync(cancellationToken);
            return probe.ExitCode == 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }

    public async Task DumpAsync(Stream destination, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        using var dump = Start("--format=custom --no-owner --no-privileges", redirectStdout: true)
            ?? throw new InvalidOperationException(
                "pg_dump اجرا نشد. ابزار پشتیبان‌گیری پایگاه‌داده روی سرور نصب نیست.");

        // stderr is drained concurrently. pg_dump writes progress and warnings
        // there, and a full pipe buffer on a channel nobody is reading is a
        // child process that stops making progress and never exits.
        var diagnostics = new StringBuilder();
        var draining = DrainAsync(dump.StandardError, diagnostics, timeout.Token);

        try
        {
            await dump.StandardOutput.BaseStream.CopyToAsync(destination, timeout.Token);
            await dump.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(dump);
            throw new InvalidOperationException(
                $"pg_dump پس از {Timeout.TotalMinutes:N0} دقیقه تمام نشد و متوقف شد.");
        }
        catch (OperationCanceledException)
        {
            Kill(dump);
            throw;
        }

        await draining;

        if (dump.ExitCode != 0)
        {
            var reason = diagnostics.ToString().Trim();
            logger.LogError("pg_dump exited with {Code}: {Reason}", dump.ExitCode, reason);

            // The tail rather than the whole log: the message is stored on the
            // job and shown on screen 156, and pg_dump can be verbose.
            throw new InvalidOperationException(
                $"pg_dump با کد {dump.ExitCode} خارج شد. {Tail(reason)}".TrimEnd());
        }
    }

    private static async Task DrainAsync(StreamReader reader, StringBuilder into, CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                into.AppendLine(line);
            }
        }
        catch (OperationCanceledException)
        {
            // The dump is being abandoned; what it was saying no longer matters.
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Exited between the check and the call.
        }
    }

    private static string Tail(string text) =>
        text.Length <= 500 ? text : text[^500..];

    private Process? Start(string arguments, bool redirectStdout)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString);

        var start = new ProcessStartInfo("pg_dump")
        {
            RedirectStandardOutput = redirectStdout,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            start.ArgumentList.Add(argument);
        }

        if (arguments != "--version")
        {
            start.ArgumentList.Add($"--host={connection.Host}");
            start.ArgumentList.Add($"--port={connection.Port}");
            start.ArgumentList.Add($"--username={connection.Username}");
            start.ArgumentList.Add($"--dbname={connection.Database}");

            // Environment, not the command line: every process on the host can
            // read another's arguments, and this one would be the database
            // password.
            start.Environment["PGPASSWORD"] = connection.Password ?? string.Empty;
        }

        return Process.Start(start);
    }
}
