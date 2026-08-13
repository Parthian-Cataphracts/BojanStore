using Bojan.Application.Diagnostics;
using Microsoft.Extensions.Options;

namespace Bojan.Infrastructure.Diagnostics;

public sealed class LogFileOptions
{
    public const string SectionName = "Logs";

    /// <summary>
    /// Directory the Serilog file sink writes into, and the only one this reads.
    /// </summary>
    /// <remarks>
    /// The same value Program.cs hands the sink. It is a container volume in a
    /// real deployment (<c>/data/logs</c>) so the record survives the restart
    /// that so often follows the thing worth reading about.
    /// </remarks>
    public string Directory { get; set; } = "logs";

    /// <summary>Most lines one request may pull back.</summary>
    /// <remarks>
    /// The panel asks for a few hundred. This is the ceiling on what anyone can
    /// ask for, because "give me the whole file" against a 32MB log is a way to
    /// make the API allocate 32MB per caller.
    /// </remarks>
    public int MaxTailLines { get; set; } = 2_000;
}

/// <summary>
/// Reads the sink's own files — see <see cref="ILogFileReader"/>.
/// </summary>
/// <remarks>
/// <para>
/// The whole security of this is <see cref="Resolve"/>, and everything public
/// goes through it. A name arrives from a browser; it is reduced to its file
/// name, checked against an extension allow-list, resolved, and then checked
/// again to confirm the resolved path is still inside the log directory. The
/// last check is the one that matters — the first two are sanity, and a symlink
/// or a clever encoding beats sanity.
/// </para>
/// <para>
/// The tail streams rather than loading. A log file is the one file on the box
/// guaranteed to be large exactly when things are going wrong, and reading it
/// whole to show the last hundred lines would turn "why did that 500" into a
/// second outage.
/// </para>
/// </remarks>
public sealed class LogFileReader(IOptions<LogFileOptions> options) : ILogFileReader
{
    private static readonly string[] Readable = [".log", ".txt", ".json"];

    private string Root => Path.GetFullPath(options.Value.Directory);

    public Task<IReadOnlyList<LogFileDto>> ListAsync(CancellationToken cancellationToken)
    {
        var root = Root;

        if (!Directory.Exists(root))
        {
            // Nothing has been written yet — a shop that has just started. An
            // empty list is the honest answer, not an error.
            return Task.FromResult<IReadOnlyList<LogFileDto>>([]);
        }

        // Top level only. The sink writes flat, and walking subdirectories would
        // be inventing a shape nothing produces.
        var files = new DirectoryInfo(root)
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(file => Readable.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new LogFileDto(file.Name, file.Length, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)))
            .ToList();

        return Task.FromResult<IReadOnlyList<LogFileDto>>(files);
    }

    public async Task<LogTailDto?> TailAsync(
        string name,
        int limit,
        string? search,
        CancellationToken cancellationToken)
    {
        if (Resolve(name) is not { } path)
        {
            return null;
        }

        var ceiling = Math.Clamp(limit, 1, options.Value.MaxTailLines);
        var needle = search?.Trim();
        var filtering = needle is { Length: > 0 };

        // A ring of the last `ceiling` matches. The file is read once, forwards,
        // and only this many lines are ever held — so the cost is the read, not
        // the size of what was read.
        var kept = new Queue<string>(ceiling);
        var seen = 0;

        // `ReadWrite` share: the sink has this file open and is appending to it.
        // Opening it exclusively would fail on the only file anybody wants.
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (filtering && !line.Contains(needle!, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            seen++;
            kept.Enqueue(line);

            if (kept.Count > ceiling)
            {
                kept.Dequeue();
            }
        }

        // Newest first: the reason somebody opened this is almost always the
        // last thing in it.
        return new LogTailDto(Path.GetFileName(path), [.. kept.Reverse()], seen > kept.Count);
    }

    /// <summary>
    /// A name from a request as an absolute path inside the log directory, or
    /// null.
    /// </summary>
    /// <remarks>
    /// The single choke point. Null covers every refusal — outside the
    /// directory, wrong extension, not there — because an endpoint that
    /// distinguishes them tells whoever is probing which of their guesses was
    /// closer.
    /// </remarks>
    private string? Resolve(string requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        // Strips any directory the caller wrote, so `../../etc/passwd` becomes
        // `passwd` before anything else looks at it.
        var bare = Path.GetFileName(requested.Trim());

        if (bare.Length == 0 || !Readable.Contains(Path.GetExtension(bare), StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var root = Root;
        var full = Path.GetFullPath(Path.Combine(root, bare));

        // And the check that actually holds: wherever the combination landed, it
        // has to still be under the root.
        var fenced = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(fenced, StringComparison.Ordinal))
        {
            return null;
        }

        return File.Exists(full) ? full : null;
    }
}
