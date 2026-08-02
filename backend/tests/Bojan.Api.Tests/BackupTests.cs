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
            Assert.NotNull(job.FileUrl);
            Assert.True(job.SizeBytes > 0);
        });

        var list = await (await _client.GetAsync("/api/admin/backups")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("completed", list[0].GetProperty("status").GetString());

        using var noRedirectClient = _factory.CreateAdminClient(_ownerId, allowAutoRedirect: false);
        var download = await noRedirectClient.GetAsync($"/api/admin/backups/{id}/download");
        Assert.True(download.StatusCode is System.Net.HttpStatusCode.Redirect or System.Net.HttpStatusCode.Found);
    }

    [Fact]
    public async Task Queuing_a_backup_without_confirmation_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/backups", new { kind = "full", confirm = false });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.Equal(0, await db.BackupJobs.CountAsync()));
    }
}
