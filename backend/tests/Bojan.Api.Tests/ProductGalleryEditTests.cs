using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Adding a picture to a product that already has one.
/// </summary>
/// <remarks>
/// <para>
/// This was refused with a conflict, and the reason had nothing to do with
/// images. Every entity here assigns its own id in its constructor, and EF
/// reads an id that is already set as proof the row exists — so a gallery row
/// created on a product loaded from the database was written as an update to a
/// row that had never been inserted. The update matched nothing, and the whole
/// save came back a 409 the operator could do nothing about.
/// </para>
/// <para>
/// The fix is one line per table (<c>ValueGeneratedNever</c>), and it is the
/// same line the categories a product is filed under need. The test is here
/// because the gallery is the older of the two and the one an operator hits
/// first.
/// </para>
/// </remarks>
public sealed class ProductGalleryEditTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _productId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "gallery", 100_000, stock: 3);
            _productId = product.Id;

            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "gallery-owner@bojan.test")).Id;
        });

        _client = _factory.CreateAdminClient(ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_second_picture_can_be_added_to_a_product_that_already_has_one()
    {
        var primary = $"https://example.test/gallery.jpg";

        // What the form posts once an operator picks another file: the whole
        // list, the image already stored included.
        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), images = new[] { primary, primary } });

        response.EnsureSuccessStatusCode();

        var gallery = await _factory.WithDbAsync(async db => await db.ProductImages.AsNoTracking()
            .Where(image => image.ProductId == _productId)
            .OrderBy(image => image.SortOrder)
            .Select(image => image.Url)
            .ToListAsync());

        // The first is the primary and lives on the product itself; the rest
        // are the gallery.
        Assert.Equal([primary], gallery);
    }
}
