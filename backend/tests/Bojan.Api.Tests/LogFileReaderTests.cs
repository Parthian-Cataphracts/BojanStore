using Bojan.Infrastructure.Diagnostics;
using Microsoft.Extensions.Options;

namespace Bojan.Api.Tests;

/// <summary>
/// The log reader, and mostly the one thing that matters about it.
/// </summary>
/// <remarks>
/// A file name arriving from a browser, used to open a file on the server, is
/// the oldest hole there is. These tests exist for the refusals rather than for
/// the reading — the reading is a queue, and the refusing is the security of
/// the whole screen.
///
/// No database, so no fixture: the reader touches a temporary directory and
/// nothing else.
/// </remarks>
public sealed class LogFileReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"bojan-logs-{Guid.NewGuid():N}");

    private readonly LogFileReader _reader;

    public LogFileReaderTests()
    {
        Directory.CreateDirectory(_root);
        _reader = new LogFileReader(Options.Create(new LogFileOptions { Directory = _root }));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp directory that will not delete is not a failing test.
        }
    }

    private string Write(string name, params string[] lines)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public async Task Lists_only_readable_extensions()
    {
        Write("bojan-20260813.log", "a");
        Write("notes.txt", "b");
        Write("archive.zip", "c");

        var files = await _reader.ListAsync(CancellationToken.None);

        Assert.Equal(
            ["bojan-20260813.log", "notes.txt"],
            files.Select(f => f.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task Missing_directory_is_no_files_rather_than_an_error()
    {
        var reader = new LogFileReader(Options.Create(new LogFileOptions
        {
            Directory = Path.Combine(_root, "never-created"),
        }));

        Assert.Empty(await reader.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Tail_returns_the_last_lines_newest_first()
    {
        Write("bojan.log", "one", "two", "three", "four");

        var tail = await _reader.TailAsync("bojan.log", 2, null, CancellationToken.None);

        Assert.NotNull(tail);
        Assert.Equal(["four", "three"], tail.Lines);
        // Two of four came back, so the screen may say so.
        Assert.True(tail.Truncated);
    }

    [Fact]
    public async Task Tail_that_covers_the_file_is_not_truncated()
    {
        Write("bojan.log", "one", "two");

        var tail = await _reader.TailAsync("bojan.log", 50, null, CancellationToken.None);

        Assert.NotNull(tail);
        Assert.Equal(["two", "one"], tail.Lines);
        Assert.False(tail.Truncated);
    }

    [Fact]
    public async Task Search_is_case_insensitive_and_filters_before_the_ceiling()
    {
        Write("bojan.log", "GET /a", "ERROR boom", "GET /b", "error again");

        var tail = await _reader.TailAsync("bojan.log", 10, "error", CancellationToken.None);

        Assert.NotNull(tail);
        Assert.Equal(["error again", "ERROR boom"], tail.Lines);
    }

    [Fact]
    public async Task Ceiling_is_capped_by_the_configured_maximum()
    {
        var reader = new LogFileReader(Options.Create(new LogFileOptions
        {
            Directory = _root,
            MaxTailLines = 2,
        }));
        Write("bojan.log", "one", "two", "three", "four");

        var tail = await reader.TailAsync("bojan.log", 1_000, null, CancellationToken.None);

        Assert.NotNull(tail);
        Assert.Equal(2, tail.Lines.Count);
    }

    /// <summary>
    /// The point of the whole class. Every one of these resolves to null, which
    /// the endpoint answers as 404 — a name that tried to leave the directory
    /// and a name that was never there are deliberately the same answer.
    /// </summary>
    [Theory]
    [InlineData("../secret.log")]
    [InlineData("..\\secret.log")]
    [InlineData("../../etc/passwd")]
    [InlineData("subdir/nested.log")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\drivers\\etc\\hosts")]
    [InlineData("bojan.log.zip")]
    [InlineData("appsettings.json.bak")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refuses_anything_that_is_not_a_log_in_the_directory(string requested)
    {
        Write("bojan.log", "real");

        Assert.Null(await _reader.TailAsync(requested, 10, null, CancellationToken.None));
    }

    [Fact]
    public async Task A_traversal_that_names_a_real_file_outside_still_refuses()
    {
        // A readable extension, a file that genuinely exists, and a path that
        // walks out of the log directory to reach it.
        var outside = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.log");
        await File.WriteAllTextAsync(outside, "not yours", CancellationToken.None);

        try
        {
            var escape = Path.Combine("..", Path.GetFileName(outside));

            Assert.Null(await _reader.TailAsync(escape, 10, null, CancellationToken.None));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task Reads_a_file_the_sink_still_has_open()
    {
        // The one file anybody wants is the one being written to. Opening it
        // exclusively would fail on exactly that file.
        var path = Path.Combine(_root, "bojan.log");
        await using var held = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        await using (var writer = new StreamWriter(held, leaveOpen: true))
        {
            await writer.WriteLineAsync("first");
            await writer.FlushAsync(CancellationToken.None);
        }

        var tail = await _reader.TailAsync("bojan.log", 10, null, CancellationToken.None);

        Assert.NotNull(tail);
        Assert.Equal(["first"], tail.Lines);
    }
}
