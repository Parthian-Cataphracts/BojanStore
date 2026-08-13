using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The server side of a round of faults reported from a running shop.
/// </summary>
/// <remarks>
/// Most of what was reported turned out to live in the panel — a form posting a
/// shape the API cannot bind, a table not drawing a field the API already
/// sends, a cache nothing ever invalidated. What is covered here is the part
/// that is this side of the wire: the export body the exporter now sends, the
/// campaign that could not be removed, and the two defaults that decided
/// whether the log screen could find anything.
/// </remarks>
public sealed class ReportedFaultsTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _owner = null!;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "faults-owner@bojan.test")).Id);

        _owner = _factory.CreateAdminClient(ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    // --- the report exporter ---------------------------------------------------

    /// <summary>
    /// The shape the panel now sends: an instant, in Tehran's offset, for the
    /// day the operator picked.
    /// </summary>
    [Fact]
    public async Task An_export_range_is_accepted_as_an_instant()
    {
        var response = await _owner.PostAsJsonAsync(
            "/api/admin/reports/export",
            new
            {
                report = "sales",
                format = "xlsx",
                from = "2026-07-23T00:00:00+03:30",
                to = "2026-08-13T23:59:59+03:30",
            });

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// What the exporter used to send, and why nobody could ever produce an
    /// export.
    /// </summary>
    /// <remarks>
    /// Both date boxes were plain text posting whatever was typed, and the
    /// operator was typing Jalali because the placeholder asked for it. It is
    /// refused during model binding, before any handler sees it, so the panel
    /// had nothing to report but its generic "ذخیره اطلاعات انجام نشد" — the
    /// screenshot that started this.
    /// </remarks>
    [Fact]
    public async Task A_jalali_date_in_the_range_is_refused_rather_than_silently_ignored()
    {
        var response = await _owner.PostAsJsonAsync(
            "/api/admin/reports/export",
            new { report = "sales", format = "xlsx", from = "1405/05/01", to = "1405/05/31" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// An untouched date box used to post <c>""</c>, which fails to bind the
    /// same way — so leaving the range empty was not a way around it either.
    /// The panel omits the field now; this holds the API to accepting that.
    /// </summary>
    [Fact]
    public async Task An_export_with_no_range_at_all_is_accepted()
    {
        var response = await _owner.PostAsJsonAsync(
            "/api/admin/reports/export",
            new { report = "orders", format = "csv" });

        response.EnsureSuccessStatusCode();
    }

    // --- removing a campaign ---------------------------------------------------

    private async Task<string> CreateCampaignAsync(string title)
    {
        var response = await _owner.PostAsJsonAsync(
            "/api/admin/campaigns",
            new { title, kind = "discount", status = "running" });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private sealed record CreatedId(string Id);

    /// <summary>
    /// <c>Campaign</c> has been a soft-deletable entity since it was written and
    /// nothing ever called either method on it, so a campaign created by
    /// mistake stayed for good — its three statuses say when it runs, not
    /// whether it should exist.
    /// </summary>
    [Fact]
    public async Task A_campaign_can_be_archived_and_leaves_the_list()
    {
        var id = await CreateCampaignAsync("کمپین اشتباهی");

        var archived = await _owner.PostAsJsonAsync(
            "/api/admin/campaigns",
            new { id, status = "archived" });
        archived.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            // Gone from the query the panel's list runs, which filters
            // soft-deleted rows out globally...
            Assert.False(await db.Campaigns.AnyAsync(c => c.Id == Guid.Parse(id)));
            // ...and still there underneath, because the reports that counted
            // its reach are not rewritten by somebody tidying up a list.
            Assert.True(await db.Campaigns.IgnoreQueryFilters().AnyAsync(c => c.Id == Guid.Parse(id)));
        });
    }

    [Fact]
    public async Task Archiving_is_reversible_by_setting_a_real_status_again()
    {
        var id = await CreateCampaignAsync("کمپین بازگشتی");

        (await _owner.PostAsJsonAsync("/api/admin/campaigns", new { id, status = "archived" }))
            .EnsureSuccessStatusCode();
        (await _owner.PostAsJsonAsync("/api/admin/campaigns", new { id, status = "scheduled" }))
            .EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.True(await db.Campaigns.AnyAsync(c => c.Id == Guid.Parse(id))));
    }

    [Fact]
    public async Task A_status_that_is_neither_a_status_nor_archived_is_still_refused()
    {
        var id = await CreateCampaignAsync("کمپین سالم");

        var response = await _owner.PostAsJsonAsync(
            "/api/admin/campaigns",
            new { id, status = "deleted" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- the log screen ---------------------------------------------------------

    /// <summary>
    /// The sink and the reader have to name the same directory when nothing is
    /// configured, and for a long time they did not.
    /// </summary>
    /// <remarks>
    /// <c>Program.cs</c> handed the sink <c>AppContext.BaseDirectory/logs</c>
    /// while the reader's option defaulted to the bare relative path
    /// <c>"logs"</c>, which resolves against the process's working directory.
    /// Two different folders, so the panel reported «فایلی برای خواندن نیست» on
    /// an installation that was writing a log perfectly well — and only on
    /// hosts that had not set <c>Logs:Directory</c>, which is why the compose
    /// deployment never showed it.
    /// </remarks>
    [Fact]
    public void The_log_directory_default_is_absolute_and_shared()
    {
        var fallback = LogFileOptions.DefaultDirectory;

        Assert.True(Path.IsPathRooted(fallback), "a relative default resolves against the working directory, which is not where the sink writes");
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "logs"), fallback);
        // The options object has to start there too — the reader binds this
        // type, and a default that only Program.cs knows is not shared.
        Assert.Equal(fallback, new LogFileOptions().Directory);
    }

    // --- outbound mail -----------------------------------------------------------

    /// <summary>
    /// A shop with no mailbox configured sends nothing, and used to say nothing
    /// about it: every screen that triggers a message reported success, because
    /// the send falls back to writing the message into the log.
    /// </summary>
    [Fact]
    public async Task The_health_board_says_when_no_mail_can_leave()
    {
        var response = await _owner.GetAsync("/api/admin/system/health");
        response.EnsureSuccessStatusCode();

        var services = await response.Content.ReadFromJsonAsync<List<HealthRow>>();
        var mail = services!.SingleOrDefault(service => service.Id == "email");

        Assert.NotNull(mail);
        // Degraded, not down: nothing is broken, a form has not been filled in.
        Assert.Equal("degraded", mail!.Status);
        Assert.False(string.IsNullOrWhiteSpace(mail.Detail));
        // And it names where to go, because "not configured" is the part the
        // owner already knew.
        Assert.Contains("صندوق پستی", mail.Detail!, StringComparison.Ordinal);
    }

    private sealed record HealthRow(string Id, string Name, string Status, int LatencyMs, DateTimeOffset CheckedAt, string? Detail);
}
