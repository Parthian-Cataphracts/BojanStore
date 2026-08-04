using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;

namespace Bojan.Api.Tests;

/// <summary>
/// Screen 146's grid, enforced.
/// </summary>
/// <remarks>
/// The grid was written, read back onto its own screen, and consulted by
/// nothing: withdrawing a section from a role saved successfully, displayed as
/// withdrawn, and changed that role's access not at all. These hold the two
/// properties that make it real — a revoked section is refused, and a grid that
/// was never configured leaves the role policies exactly as they were.
/// </remarks>
public sealed class SectionPermissionTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _productOperator;
    private Guid _owner;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            await TestData.AddCatalogueAsync(db);
            _productOperator = (await TestData.AddAdminAsync(db, AdminRole.Product, "product@section.test")).Id;
            _owner = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@section.test")).Id;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    private async Task GrantAsync(params string[] sections)
    {
        using var owner = _factory.CreateAdminClient(_owner);

        var response = await owner.PostAsJsonAsync("/api/admin/roles/permissions", new
        {
            grants = PanelSection.All.Select(section => new
            {
                role = "product",
                section,
                granted = sections.Contains(section),
            }),
        });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task With_no_grid_configured_the_role_policy_still_decides()
    {
        using var client = _factory.CreateAdminClient(_productOperator);

        // Nothing has been saved on screen 146, so an installation that never
        // opened it must behave exactly as it did before the grid existed.
        var response = await client.GetAsync("/api/admin/products");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_granted_section_is_reachable()
    {
        await GrantAsync(PanelSection.Products);

        using var client = _factory.CreateAdminClient(_productOperator);
        var response = await client.GetAsync("/api/admin/products");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_revoked_section_is_refused()
    {
        // Configured, but products withheld from this role.
        await GrantAsync(PanelSection.Content);

        using var client = _factory.CreateAdminClient(_productOperator);

        var read = await client.GetAsync("/api/admin/products");
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        // The write half too — a grid that only hid the list would leave the
        // save button working for anyone who knew the path.
        var write = await client.PostAsJsonAsync("/api/admin/products", new { title = "محصول تازه" });
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    /// <summary>
    /// The grid narrows the role policies; it cannot widen them.
    /// </summary>
    [Fact]
    public async Task Granting_a_section_the_role_policy_forbids_gives_nothing()
    {
        await GrantAsync(PanelSection.Products, PanelSection.Settings);

        using var client = _factory.CreateAdminClient(_productOperator);

        // `settings` is owner-only at the policy, whatever the grid says.
        var response = await client.GetAsync("/api/admin/settings/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// A panel whose full-access role can be shut out of settings is one save
    /// away from being unadministrable.
    /// </summary>
    [Fact]
    public async Task The_owner_is_never_gated_by_the_grid()
    {
        await GrantAsync();

        using var client = _factory.CreateAdminClient(_owner);

        var response = await client.GetAsync("/api/admin/settings/audit");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_section_that_is_not_a_known_key_is_refused()
    {
        using var owner = _factory.CreateAdminClient(_owner);

        var response = await owner.PostAsJsonAsync("/api/admin/roles/permissions", new
        {
            grants = new[] { new { role = "product", section = "سفارش‌ها", granted = true } },
        });

        // The grid used to post its Persian labels and they were stored as
        // given, which made a permission depend on a display string.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
