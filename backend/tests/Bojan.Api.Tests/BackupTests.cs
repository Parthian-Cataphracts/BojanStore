using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bojan.Api.Tests;

/// <summary>
/// Screen 156 — the backup takes a real one, or says it did not.
/// </summary>
/// <remarks>
/// What this replaces wrote a small JSON file naming the job — its id, kind,
/// who asked and when — marked the row completed and offered it for download.
/// No database was dumped, no uploaded file was archived, and the operator got
/// every signal that they were protected. The tests below are about the two
/// halves of fixing that: the archive contains what it claims to, and a backup
/// that cannot be taken is recorded as failed rather than finished.
/// </remarks>
public sealed class BackupTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _ownerId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            _ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test")).Id;
        });

        _client = _factory.CreateAdminClient(_ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    /// <summary>Queues a backup and returns its id, without running it.</summary>
    private async Task<string> QueueAsync(string kind = "full")
    {
        var response = await _client.PostAsJsonAsync("/api/admin/backups", new { kind, confirm = true });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Drives one worker cycle. The host does not run background services —
    /// see <c>BojanApiFactory</c> — so the queue is drained explicitly.
    /// </summary>
    private Task DrainAsync() =>
        new BackupWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BackupWorker>.Instance)
            .RunOnceAsync(CancellationToken.None);

    private async Task<BackupJob> JobAsync()
    {
        BackupJob job = null!;
        await _factory.WithDbAsync(async db => job = await db.BackupJobs.SingleAsync());
        return job;
    }

    [Fact]
    public async Task Queuing_leaves_the_job_waiting_rather_than_claiming_it_is_done()
    {
        await QueueAsync();

        var job = await JobAsync();
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Null(job.ArchiveReference);
    }

    [Fact]
    public async Task The_archive_actually_contains_the_database_dump_and_the_media()
    {
        // A file in the uploads tree, so the media half has something to find.
        var uploads = Path.Combine(AppContext.BaseDirectory, "uploads", "products");
        Directory.CreateDirectory(uploads);
        await File.WriteAllTextAsync(Path.Combine(uploads, "photo.txt"), "not really a photo");

        var id = await QueueAsync("full");
        await DrainAsync();

        var job = await JobAsync();
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Null(job.Error);
        Assert.True(job.SizeBytes > 0);
        Assert.Equal(1, _factory.Dumper.Dumps);

        var download = await _client.GetAsync($"/api/admin/backups/{id}/download");
        download.EnsureSuccessStatusCode();
        Assert.Equal("application/zip", download.Content.Headers.ContentType?.MediaType);

        using var archive = new ZipArchive(await download.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);

        var dump = archive.GetEntry("database.dump");
        Assert.NotNull(dump);
        Assert.Equal(_factory.Dumper.Payload.Length, dump.Length);

        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("media/", StringComparison.Ordinal));
    }

    /// <summary>
    /// The regression that matters most: no tooling, no backup, and the panel
    /// says so.
    /// </summary>
    [Fact]
    public async Task A_backup_that_cannot_be_taken_is_failed_and_not_downloadable()
    {
        _factory.Dumper.Available = false;

        var id = await QueueAsync("database");
        await DrainAsync();

        var job = await JobAsync();
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Null(job.ArchiveReference);
        Assert.Null(job.SizeBytes);
        Assert.Contains("pg_dump", job.Error);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/admin/backups/{id}/download")).StatusCode);

        var list = await (await _client.GetAsync("/api/admin/backups")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("failed", list[0].GetProperty("status").GetString());
        Assert.False(list[0].GetProperty("downloadable").GetBoolean());
    }

    /// <summary>A dump that dies halfway leaves no archive behind to be mistaken for one.</summary>
    [Fact]
    public async Task A_dump_that_fails_partway_leaves_nothing_downloadable()
    {
        _factory.Dumper.FailWith = "connection to server was lost";

        var id = await QueueAsync("database");
        await DrainAsync();

        var job = await JobAsync();
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Null(job.ArchiveReference);
        Assert.Contains("connection to server was lost", job.Error);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/admin/backups/{id}/download")).StatusCode);
    }

    /// <summary>
    /// The archive lives outside anything a public media root would serve —
    /// the reference is a bare filename, the only thing IBackupArchiver hands
    /// back.
    /// </summary>
    [Fact]
    public async Task The_archive_reference_is_never_a_public_path()
    {
        await QueueAsync("media");
        await DrainAsync();

        var job = await JobAsync();
        Assert.NotNull(job.ArchiveReference);
        Assert.DoesNotContain('/', job.ArchiveReference);
        Assert.DoesNotContain("media/", job.ArchiveReference, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Queuing_a_backup_without_confirmation_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/backups", new { kind = "full", confirm = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.Equal(0, await db.BackupJobs.CountAsync()));
    }

    /// <summary>
    /// A kind nothing can produce is refused at the door rather than queued and
    /// then failed — the second reads as the shop's backups being broken.
    /// </summary>
    [Fact]
    public async Task An_unknown_backup_kind_is_refused()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/backups", new { kind = "everything", confirm = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.Equal(0, await db.BackupJobs.CountAsync()));
    }
}
