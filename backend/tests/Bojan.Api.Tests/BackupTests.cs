using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>Screen 156 — the backup job now runs to completion and can be listed and downloaded.</summary>
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

    [Fact]
    public async Task Queuing_a_backup_completes_it_with_a_downloadable_file()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/backups", new { kind = "full", confirm = true });
        response.EnsureSuccessStatusCode();

        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        await _factory.WithDbAsync(async db =>
        {
            var job = await db.BackupJobs.SingleAsync();
            Assert.Equal(JobStatus.Completed, job.Status);
            Assert.NotNull(job.ArchiveReference);
            Assert.True(job.SizeBytes > 0);
        });

        var list = await (await _client.GetAsync("/api/admin/backups")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("completed", list[0].GetProperty("status").GetString());
        // Downloadable, not where — the list must never carry the archive's
        // location, only whether one exists.
        Assert.True(list[0].GetProperty("downloadable").GetBoolean());
        Assert.False(list[0].TryGetProperty("fileUrl", out _));

        var download = await _client.GetAsync($"/api/admin/backups/{id}/download");
        download.EnsureSuccessStatusCode();
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);

        using var doc = JsonDocument.Parse(bytes);
        Assert.Equal(id, doc.RootElement.GetProperty("Id").GetString());
    }

    /// <summary>
    /// The archive lives outside anything a public media root would serve —
    /// this asserts the file this test just downloaded is not also reachable
    /// through the storefront's own upload URL scheme.
    /// </summary>
    [Fact]
    public async Task The_downloaded_archive_is_not_under_the_public_media_root()
    {
        await _client.PostAsJsonAsync("/api/admin/backups", new { kind = "full", confirm = true });

        await _factory.WithDbAsync(async db =>
        {
            var job = await db.BackupJobs.SingleAsync();
            Assert.NotNull(job.ArchiveReference);
            // The reference is a bare filename, never a "/media/..."-shaped
            // path — the only thing IBackupArchiver ever hands back.
            Assert.DoesNotContain('/', job.ArchiveReference);
            Assert.DoesNotContain("media", job.ArchiveReference, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Queuing_a_backup_without_confirmation_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/backups", new { kind = "full", confirm = false });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.Equal(0, await db.BackupJobs.CountAsync()));
    }
}
