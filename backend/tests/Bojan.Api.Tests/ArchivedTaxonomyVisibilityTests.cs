using System.Net.Http.Json;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Archiving a brand or a category used to empty the shelf under it.
/// </summary>
/// <remarks>
/// Both carry a soft-delete query filter, and the catalogue projection joined
/// to them with an inner join — so the filter became part of the join
/// condition. Every product of an archived brand disappeared from the
/// storefront while the panel went on listing it as published: no error, no log
/// line, and nothing on either screen to connect the two.
/// </remarks>
public sealed class ArchivedTaxonomyVisibilityTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _brandId;
    private Guid _categoryId;
    private string _slug = string.Empty;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            (_brandId, _categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, _brandId, _categoryId, "archived-taxonomy", 120_000, 4);
            _slug = product.Slug;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_product_stays_listed_when_its_brand_is_archived()
    {
        Assert.Equal(1, (await ListAsync()).Total);

        await ArchiveBrandAsync();

        Assert.Equal(1, (await ListAsync()).Total);
    }

    [Fact]
    public async Task A_product_stays_listed_when_its_category_is_archived()
    {
        Assert.Equal(1, (await ListAsync()).Total);

        await _factory.WithDbAsync(async db =>
        {
            var category = await db.Categories.FirstAsync(c => c.Id == _categoryId);
            category.SoftDelete(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        Assert.Equal(1, (await ListAsync()).Total);
    }

    [Fact]
    public async Task A_product_detail_page_still_answers_when_its_brand_is_archived()
    {
        await ArchiveBrandAsync();

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/products/{_slug}");

        response.EnsureSuccessStatusCode();

        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(product);

        // The brand name is empty rather than the product being unreachable.
        // An archived brand is one the shop no longer promotes, not a shelf it
        // silently empties.
        Assert.Equal(string.Empty, product!.Brand);
    }

    private Task ArchiveBrandAsync() =>
        _factory.WithDbAsync(async db =>
        {
            var brand = await db.Brands.FirstAsync(b => b.Id == _brandId);
            brand.SoftDelete(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

    private async Task<Paged<ProductDto>> ListAsync()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/products?pageSize=100");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Paged<ProductDto>>())!;
    }
}
