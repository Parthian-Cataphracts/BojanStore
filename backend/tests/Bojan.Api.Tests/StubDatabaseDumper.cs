using System.Text;
using Bojan.Application.Common;

namespace Bojan.Api.Tests;

/// <summary>
/// Stands in for <c>pg_dump</c>.
/// </summary>
/// <remarks>
/// These tests run against SQLite, so the real runner has nothing to dump and
/// no PostgreSQL to reach. What is worth proving here is the worker's own
/// behaviour — that a dump lands inside the archive, and that a dumper which
/// cannot run produces a job marked failed with a reason rather than one marked
/// completed over an empty file. Both are the same code path whichever dumper
/// is behind the port.
/// </remarks>
public sealed class StubDatabaseDumper : IDatabaseDumper
{
    /// <summary>What <c>IsAvailableAsync</c> answers — false is "pg_dump is not installed".</summary>
    public bool Available { get; set; } = true;

    /// <summary>Set to make the dump itself fail after starting, as a broken one would.</summary>
    public string? FailWith { get; set; }

    public byte[] Payload { get; set; } = Encoding.UTF8.GetBytes("-- a database dump would be here\n");

    public int Dumps { get; private set; }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(Available);

    public async Task DumpAsync(Stream destination, CancellationToken cancellationToken)
    {
        Dumps++;

        if (FailWith is { } reason)
        {
            // Partly written first, so the cleanup path is exercised the way a
            // dump that dies halfway through would exercise it.
            await destination.WriteAsync(Payload.AsMemory(0, Payload.Length / 2), cancellationToken);
            throw new InvalidOperationException(reason);
        }

        await destination.WriteAsync(Payload, cancellationToken);
    }
}
