namespace Bojan.Application.Diagnostics;

/// <summary>One log file on disk, as the panel lists it.</summary>
/// <param name="SizeBytes">
/// Shown so somebody can tell a file that has been rolling all day from one
/// written in the minute after a restart, without opening either.
/// </param>
public sealed record LogFileDto(string Name, long SizeBytes, DateTimeOffset ModifiedAtUtc);

/// <summary>
/// One line, split into the parts a reader actually scans for.
/// </summary>
/// <remarks>
/// Parsed here rather than in the browser because the format is the sink's and
/// the panel should not have to know it. <paramref name="At"/> and
/// <paramref name="Level"/> are null on a line that carries neither — the
/// continuation lines of a stack trace, which are the most important lines in
/// the file and the ones that fit no format at all.
/// </remarks>
/// <param name="Raw">
/// The line exactly as written. Everything else here is derived, and when the
/// derivation is wrong this is what the reader falls back to.
/// </param>
public sealed record LogLineDto(DateTimeOffset? At, string? Level, string Message, string Raw);

/// <summary>
/// The end of one log file.
/// </summary>
/// <param name="Lines">
/// Newest first. A log is read backwards — the reason somebody opened it is
/// almost always the last thing in it.
/// </param>
/// <param name="Matched">
/// How many lines matched in the whole file, which is not how many came back.
/// The difference is what lets the screen say "the last 300 of 4,812" rather
/// than implying the file is 300 lines long.
/// </param>
public sealed record LogTailDto(
    string Name,
    IReadOnlyList<LogLineDto> Lines,
    int Matched,
    bool Truncated);

/// <summary>
/// Reads what the application has written about itself.
/// </summary>
/// <remarks>
/// <para>
/// Its own port rather than a reach into the file system from an endpoint,
/// because everything about this is a question of *which* files may be read and
/// that answer belongs in one place. A path arriving from a browser and a
/// directory of server logs is the oldest hole in the book.
/// </para>
/// <para>
/// Reading only. There is no delete and no write: the retention policy is the
/// sink's, and an operator who could clear the log could clear the record of
/// what they did — which is the one thing a log exists to prevent.
/// </para>
/// </remarks>
public interface ILogFileReader
{
    /// <summary>The readable log files, newest first.</summary>
    Task<IReadOnlyList<LogFileDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The last <paramref name="limit"/> lines of one file, optionally only
    /// those containing <paramref name="search"/>.
    /// </summary>
    /// <returns>
    /// Null when the name does not resolve to a readable log file — a missing
    /// file and a name that tried to leave the directory are the same answer,
    /// because telling them apart tells a prober which is which.
    /// </returns>
    Task<LogTailDto?> TailAsync(string name, int limit, string? search, CancellationToken cancellationToken);

    /// <summary>
    /// One file as an absolute path, for streaming to the browser, or null.
    /// </summary>
    /// <remarks>
    /// The same choke point the rest of this goes through. Downloading is where
    /// a traversal would be worth the most, so it does not get its own
    /// resolution rule.
    /// </remarks>
    string? ResolveForDownload(string name);

    /// <summary>Every readable file, absolute, for an archive of the lot.</summary>
    IReadOnlyList<string> AllForDownload();
}
