using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>Screen 146 — the role×section grid now has a real backend behind it.</summary>
public sealed class RolePermissionTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
        {
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test")).Id;
        });

        _client = _factory.CreateAdminClient(ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    /// <remarks>
    /// The sections are the stable keys <c>PanelSection</c> declares. They used
    /// to be whatever string the grid posted, which was its Persian column
    /// label — so a permission depended on a display string surviving a
    /// rewording, and the API had no way to tell a real section from a typo.
    /// </remarks>
    [Fact]
    public async Task Saving_the_matrix_replaces_the_whole_grant_set()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/roles/permissions", new
        {
            grants = new[]
            {
                new { role = "product", section = PanelSection.Products, granted = true },
                new { role = "product", section = PanelSection.Orders, granted = false },
                new { role = "sales", section = PanelSection.Orders, granted = true },
            },
        });
        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var grants = await db.RolePermissions.ToListAsync();
            Assert.Equal(2, grants.Count);
            Assert.Contains(grants, g => g.Role == "product" && g.Section == PanelSection.Products);
            Assert.Contains(grants, g => g.Role == "sales" && g.Section == PanelSection.Orders);
        });

        var list = await (await _client.GetAsync("/api/admin/roles/permissions"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, list.GetArrayLength());

        // Saving again with a smaller set drops what the first save granted —
        // this is a replace, not a merge.
        var second = await _client.PostAsJsonAsync("/api/admin/roles/permissions", new
        {
            grants = new[] { new { role = "product", section = PanelSection.Products, granted = true } },
        });
        second.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.Equal(1, await db.RolePermissions.CountAsync()));
    }

    [Fact]
    public async Task Granting_the_owner_role_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/roles/permissions", new
        {
            grants = new[] { new { role = "owner", section = PanelSection.Orders, granted = true } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.Equal(0, await db.RolePermissions.CountAsync()));
    }
}
