using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;

namespace Bojan.Api.Tests;

/// <summary>
/// A write the database refuses used to reach the client as a 500.
/// </summary>
/// <remarks>
/// Ten unique indexes guard this schema and every writer checked first and then
/// inserted; nothing caught the violation when two requests passed that check
/// at once, or when one request carried the same value twice.
/// <c>DbUpdateException</c> appeared nowhere in the backend.
/// </remarks>
public sealed class PersistenceConflictTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _owner;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            _owner = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@conflict.test")).Id;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_duplicated_permission_cell_is_a_conflict_rather_than_a_server_error()
    {
        using var owner = _factory.CreateAdminClient(_owner);

        // The grid posts one row per cell. Two rows for the same cell violate
        // the unique index on (Role, Section) — a caller's mistake, not ours.
        var response = await owner.PostAsJsonAsync("/api/admin/roles/permissions", new
        {
            grants = new[]
            {
                new { role = "sales", section = PanelSection.Orders, granted = true },
                new { role = "sales", section = PanelSection.Orders, granted = true },
            },
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task The_response_names_no_constraint_or_column()
    {
        using var owner = _factory.CreateAdminClient(_owner);

        var response = await owner.PostAsJsonAsync("/api/admin/roles/permissions", new
        {
            grants = new[]
            {
                new { role = "sales", section = PanelSection.Orders, granted = true },
                new { role = "sales", section = PanelSection.Orders, granted = true },
            },
        });

        var body = await response.Content.ReadAsStringAsync();

        // Constraint names and column names stay in the log.
        Assert.DoesNotContain("IX_", body, StringComparison.Ordinal);
        Assert.DoesNotContain("role_permissions", body, StringComparison.Ordinal);
        Assert.Contains("conflict", body, StringComparison.Ordinal);
    }
}
