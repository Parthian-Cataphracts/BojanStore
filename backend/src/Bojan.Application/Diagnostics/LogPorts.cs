namespace Bojan.Application.Diagnostics;

/// <summary>One log file on disk, as the panel lists it.</summary>
/// <param name="SizeBytes">
/// Shown so somebody can tell a file that has been rolling all day from one
/// written in the minute after a restart, without opening either.
/// </param>
public sealed record LogFileDto(string Name, long SizeBytes, DateTimeOffset ModifiedAtUtc);

/// <summary>
/// The end of one log file.
/// </summary>
/// <param name="Lines">
/// Newest first. A log is read backwards — the reason somebody opened it is
/// almost always the last thing in it.
/// </param>
/// <param name="Truncated">
/// Whether the ceiling was reached, so the screen can say "these are the last
/// N" rather than implying the file is this short.
/// </param>
public sealed record LogTailDto(string Name, IReadOnlyList<string> Lines, bool Truncated);

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
}
