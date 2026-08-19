using System.Net.Http.Json;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The number on a category tile is the number of products browsing it shows.
/// </summary>
/// <remarks>
/// <para>
/// A parent's count is its whole subtree, because selecting a parent lists its
/// children's products too. That used to be the parent's own count plus each
/// child's — correct only while a product had exactly one category, which is
/// no longer true. A product filed under both a parent and a child of it, or
/// under two children of the same parent, was counted once per filing while
/// the listing under the tile showed it once.
/// </para>
/// <para>
/// Every case here asserts the tile against the listing rather than against a
/// literal, because agreeing with each other is the actual requirement.
/// </para>
/// </remarks>
public sealed class CategoryProductCountTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _brandId;
    private Guid _parentId;
    private Guid _firstChildId;
    private Guid _secondChildId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            (_brandId, _parentId) = await TestData.AddCatalogueAsync(db);

            var first = new Category { Slug = "child-a", Name = "فرزند الف", Icon = "edit", ParentId = _parentId };
            var second = new Category { Slug = "child-b", Name = "فرزند ب", Icon = "edit", ParentId = _parentId };
            db.Categories.AddRange(first, second);
            await db.SaveChangesAsync();

            _firstChildId = first.Id;
            _secondChildId = second.Id;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_product_in_both_a_parent_and_its_child_is_counted_once()
    {
        await FileAsync("both", _parentId, _firstChildId);

        Assert.Equal(1, await TileAsync("test-category"));
        Assert.Equal(1, await BrowseTotalAsync("test-category"));
    }

    [Fact]
    public async Task A_product_in_two_children_of_one_parent_is_counted_once_by_that_parent()
    {
        await FileAsync("shared", _firstChildId, _secondChildId);

        Assert.Equal(1, await TileAsync("test-category"));
        Assert.Equal(1, await BrowseTotalAsync("test-category"));

        // Each child still counts it, because browsing either child shows it.
        Assert.Equal(1, await TileAsync("child-a"));
        Assert.Equal(1, await TileAsync("child-b"));
    }

    [Fact]
    public async Task Separate_products_in_a_parent_and_its_child_still_add_up()
    {
        await FileAsync("direct", _parentId);
        await FileAsync("nested", _firstChildId);

        Assert.Equal(2, await TileAsync("test-category"));
        Assert.Equal(2, await BrowseTotalAsync("test-category"));
        Assert.Equal(1, await TileAsync("child-a"));
    }

    [Fact]
    public async Task A_draft_product_is_on_no_tile_and_in_no_listing()
    {
        await _factory.WithDbAsync(async db =>
        {
            await TestData.AddProductAsync(db, _brandId, _parentId, "unreleased", 1000, 1, published: false);
        });

        Assert.Equal(0, await TileAsync("test-category"));
        Assert.Equal(0, await BrowseTotalAsync("test-category"));
    }

    private Task FileAsync(string slug, params Guid[] categoryIds) =>
        _factory.WithDbAsync(async db =>
        {
            var product = await TestData.AddProductAsync(db, _brandId, categoryIds[0], slug, 1000, 1);
            product.ReplaceCategories(categoryIds);
            await db.SaveChangesAsync();
        });

    private async Task<int> TileAsync(string slug)
    {
        using var client = _factory.CreateClient();
        var categories = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        Assert.NotNull(categories);

        var found = categories.FirstOrDefault(c => c.Slug == slug)
            ?? categories.SelectMany(c => c.Children ?? []).FirstOrDefault(c => c.Slug == slug);

        // A category nothing is filed under is not published away — it is on
        // the list with a zero.
        Assert.NotNull(found);
        return found.ProductCount;
    }

    private async Task<int> BrowseTotalAsync(string slug)
    {
        using var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<Paged<ProductDto>>($"/api/products?category={slug}");
        Assert.NotNull(page);
        return page.Total;
    }
}
