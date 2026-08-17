using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Grants exactly these sections to the operator under test.
    /// </summary>
    /// <remarks>
    /// Written against the operator rather than posted to the old role grid,
    /// because the grid is not what narrows access any more: a permission
    /// belongs to a person now, so granting «products» to the product role
    /// would have granted it to every product operator and this fixture could
    /// not have told one from another.
    ///
    /// Rows rather than an endpoint call so the fixture states the end state
    /// plainly — what is being tested below is the filter, not the screen that
    /// writes for it.
    /// </remarks>
    private async Task GrantAsync(params string[] sections)
    {
        await _factory.WithDbAsync(async db =>
        {
            var existing = await db.AdminUserSections
                .Where(s => s.AdminUserId == _productOperator)
                .ToListAsync();
            db.AdminUserSections.RemoveRange(existing);

            foreach (var section in sections)
            {
                db.AdminUserSections.Add(new AdminUserSection
                {
                    AdminUserId = _productOperator,
                    Section = section,
                });
            }

            await db.SaveChangesAsync();
        });
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

    /// <summary>
    /// The operator upload route is gated by the folder it names, which is what
    /// says where the file is going.
    /// </summary>
    /// <remarks>
    /// Without that it was the one operator write the grid could not reach: a
    /// role with the catalogue withdrawn could still put images into it.
    /// </remarks>
    [Fact]
    public async Task Uploading_into_a_revoked_section_is_refused()
    {
        await GrantAsync(PanelSection.Content);

        using var client = _factory.CreateAdminClient(_productOperator);
        using var content = new MultipartFormDataContent();

        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "image.png");

        var refused = await client.PostAsync("/api/admin/uploads/products", content);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        // The folder this role does hold still works.
        using var allowed = new MultipartFormDataContent();
        var second = new ByteArrayContent(png);
        second.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        allowed.Add(second, "file", "image.png");

        var accepted = await client.PostAsync("/api/admin/uploads/content", allowed);
        accepted.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A grant on one screen opens the section that screen reads from.
    /// </summary>
    /// <remarks>
    /// This is the coarseness the finer key deliberately keeps — see
    /// <see cref="PanelScreen"/>. What «مرجوعی‌ها, not سفارش‌ها» buys is a panel
    /// that shows one and not the other; the endpoints underneath both are the
    /// order section, and narrowing those would mean naming a screen on each of
    /// a hundred and fifty routes.
    /// </remarks>
    [Fact]
    public async Task A_screen_grant_opens_the_section_behind_it()
    {
        await GrantAsync("/categories");

        using var client = _factory.CreateAdminClient(_productOperator);

        var withinTheSection = await client.GetAsync("/api/admin/products");
        withinTheSection.EnsureSuccessStatusCode();

        // Another section is still refused: the grant narrows to one area, it
        // does not stop being a narrowing.
        var elsewhere = await client.GetAsync("/api/admin/content");
        Assert.Equal(HttpStatusCode.Forbidden, elsewhere.StatusCode);
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
