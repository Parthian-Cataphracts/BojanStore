using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The export queue used to be a way around the read gates.
/// </summary>
/// <remarks>
/// <c>GET /admin/reports/financial</c> is owner-only, but
/// <c>POST /admin/reports/export</c> and the download beside it were open to
/// every role and the report name was taken as a free string. A support
/// operator could queue "financial", wait for the worker, and download figures
/// their own role is refused. The download had no ownership check either, so
/// any operator holding an id could read a colleague's export.
/// </remarks>
public sealed class ReportExportAuthorizationTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _owner;
    private Guid _support;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            _owner = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@export.test")).Id;
            _support = (await TestData.AddAdminAsync(db, AdminRole.Support, "support@export.test")).Id;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    private static readonly Func<object> FinancialExport = () => new { report = "financial", format = "csv" };

    [Fact]
    public async Task A_non_owner_cannot_queue_the_owner_only_financial_report()
    {
        using var support = _factory.CreateAdminClient(_support);

        var response = await support.PostAsJsonAsync("/api/admin/reports/export", FinancialExport());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_owner_may_queue_the_financial_report()
    {
        using var owner = _factory.CreateAdminClient(_owner);

        var response = await owner.PostAsJsonAsync("/api/admin/reports/export", FinancialExport());

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Any_operator_may_still_queue_a_report_their_role_can_read()
    {
        using var support = _factory.CreateAdminClient(_support);

        var response = await support.PostAsJsonAsync(
            "/api/admin/reports/export", new { report = "orders", format = "csv" });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task An_unknown_report_is_refused_at_the_door_rather_than_failing_in_the_worker()
    {
        using var owner = _factory.CreateAdminClient(_owner);

        var response = await owner.PostAsJsonAsync(
            "/api/admin/reports/export", new { report = "everything", format = "csv" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_export_is_downloadable_only_by_the_operator_who_queued_it()
    {
        using var owner = _factory.CreateAdminClient(_owner);
        using var support = _factory.CreateAdminClient(_support);

        var queued = await owner.PostAsJsonAsync("/api/admin/reports/export", FinancialExport());
        queued.EnsureSuccessStatusCode();
        var id = (await queued.Content.ReadFromJsonAsync<QueuedExport>())!.Id;

        // Stand in for the worker: the download only answers once a file exists,
        // and this test is about who may read it, not about how it is produced.
        await _factory.WithDbAsync(async db =>
        {
            var export = await db.ReportExports.FirstAsync(e => e.Id == Guid.Parse(id));
            export.FileUrl = "financial-test.csv";
            export.Status = JobStatus.Completed;
            await db.SaveChangesAsync();
        });

        var theirs = await support.GetAsync($"/api/admin/reports/export/{id}/download");

        // Not-found rather than forbidden: an id that exists must not be
        // distinguishable from one that does not.
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
    }

    private sealed record QueuedExport(string Id);
}
