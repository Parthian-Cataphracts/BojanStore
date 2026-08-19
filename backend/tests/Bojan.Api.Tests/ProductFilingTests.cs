using System.Net;
using System.Net.Http.Json;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Admin;
using Bojan.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// A product may be filed under several categories and belong to several
/// collections, and the panel is where both are set.
/// </summary>
/// <remarks>
/// <para>
/// One <c>CategoryId</c> was the whole story: a notebook that is both
/// stationery and a gift had to be filed under one of them, and browsing the
/// other did not have it. Collections were worse — membership existed in the
/// schema and had no write path at all, so a curated grouping could be created
/// in the panel and never filled.
/// </para>
/// <para>
/// The primary category stays single-valued, because the breadcrumb and the
/// product card each have room for exactly one answer. What these check is
/// that the primary is always inside the set, and that browsing reads the set
/// rather than the primary.
/// </para>
/// </remarks>
public sealed class ProductFilingTests : IAsyncLifetime, IDisposable
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

            db.Categories.Add(new Category { Slug = "gifts", Name = "هدیه", Icon = "redeem" });
            db.Collections.Add(new Collection { Slug = "desk", Title = "میز کار" });
            db.Collections.Add(new Collection { Slug = "gifting", Title = "هدیه دادن" });
            await db.SaveChangesAsync();

            var product = await TestData.AddProductAsync(db, brandId, categoryId, "notebook", 120_000, stock: 4);
            _productId = product.Id;

            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "filing-owner@bojan.test")).Id;
        });

        _client = _factory.CreateAdminClient(ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_product_can_be_filed_under_more_than_one_category()
    {
        var response = await SaveAsync(new[] { "test-category", "gifts" });
        response.EnsureSuccessStatusCode();

        var filings = await FilingsAsync();

        Assert.Equal(["test-category", "gifts"], filings);
    }

    [Fact]
    public async Task Browsing_the_second_category_finds_the_product()
    {
        // Nothing is filed under "gifts" to begin with, so a listing that came
        // back with the product now would prove nothing about the save.
        Assert.Equal(0, (await BrowseAsync("gifts")).Total);

        (await SaveAsync(new[] { "test-category", "gifts" })).EnsureSuccessStatusCode();

        Assert.Equal(1, (await BrowseAsync("gifts")).Total);

        // And it has not fallen out of the one it was already in.
        Assert.Equal(1, (await BrowseAsync("test-category")).Total);
    }

    [Fact]
    public async Task The_first_category_picked_is_the_primary_one()
    {
        (await SaveAsync(new[] { "gifts", "test-category" })).EnsureSuccessStatusCode();

        var primary = await _factory.WithDbAsync(async db =>
        {
            var product = await db.Products.AsNoTracking().FirstAsync(p => p.Id == _productId);
            return await db.Categories.AsNoTracking()
                .Where(c => c.Id == product.CategoryId)
                .Select(c => c.Slug)
                .FirstAsync();
        });

        Assert.Equal("gifts", primary);
    }

    [Fact]
    public async Task Unticking_a_category_takes_the_product_out_of_it()
    {
        (await SaveAsync(new[] { "test-category", "gifts" })).EnsureSuccessStatusCode();
        (await SaveAsync(new[] { "gifts" })).EnsureSuccessStatusCode();

        Assert.Equal(["gifts"], await FilingsAsync());
        Assert.Equal(0, (await BrowseAsync("test-category")).Total);
    }

    [Fact]
    public async Task A_product_filed_nowhere_is_refused()
    {
        var response = await SaveAsync([]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // And the filing it had is untouched — a refusal that half-applied
        // would be worse than either outcome.
        Assert.Equal(["test-category"], await FilingsAsync());
    }

    [Fact]
    public async Task A_category_that_names_nothing_refuses_the_whole_save()
    {
        var response = await SaveAsync(new[] { "test-category", "no-such-category" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["test-category"], await FilingsAsync());
    }

    [Fact]
    public async Task The_same_category_twice_is_one_filing()
    {
        (await SaveAsync(new[] { "gifts", "gifts", "test-category" })).EnsureSuccessStatusCode();

        Assert.Equal(["gifts", "test-category"], await FilingsAsync());
    }

    [Fact]
    public async Task A_product_can_be_put_into_several_collections_from_its_own_form()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), collections = new[] { "desk", "gifting" } });

        response.EnsureSuccessStatusCode();

        Assert.Equal(["desk", "gifting"], await MembershipsAsync());
    }

    [Fact]
    public async Task Clearing_the_list_takes_the_product_out_of_every_collection()
    {
        await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), collections = new[] { "desk", "gifting" } });

        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), collections = Array.Empty<string>() });

        response.EnsureSuccessStatusCode();

        Assert.Empty(await MembershipsAsync());
    }

    [Fact]
    public async Task A_save_that_says_nothing_about_collections_leaves_them_alone()
    {
        await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), collections = new[] { "desk" } });

        // The pricing fields and nothing else — the shape half the panel's
        // other screens post.
        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), price = 130_000 });

        response.EnsureSuccessStatusCode();

        Assert.Equal(["desk"], await MembershipsAsync());
    }

    [Fact]
    public async Task A_collection_that_names_nothing_refuses_the_whole_save()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), collections = new[] { "desk", "no-such-collection" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await MembershipsAsync());
    }

    [Fact]
    public async Task Joining_a_collection_lands_the_product_at_the_end_of_it()
    {
        Guid otherProductId = default;

        await _factory.WithDbAsync(async db =>
        {
            var brandId = await db.Brands.AsNoTracking().Select(b => b.Id).FirstAsync();
            var categoryId = await db.Categories.AsNoTracking()
                .Where(c => c.Slug == "test-category")
                .Select(c => c.Id)
                .FirstAsync();

            var other = await TestData.AddProductAsync(db, brandId, categoryId, "pencil", 40_000, stock: 9);
            otherProductId = other.Id;
        });

        await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = otherProductId.ToString(), collections = new[] { "desk" } });

        await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), collections = new[] { "desk" } });

        var order = await _factory.WithDbAsync(async db => await db.CollectionProducts.AsNoTracking()
            .OrderBy(membership => membership.SortOrder)
            .Select(membership => membership.ProductId)
            .ToListAsync());

        Assert.Equal([otherProductId, _productId], order);
    }

    [Fact]
    public async Task The_panel_reads_back_what_the_form_posted()
    {
        (await SaveAsync(new[] { "gifts", "test-category" })).EnsureSuccessStatusCode();

        await _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), collections = new[] { "desk" } });

        var product = await _client.GetFromJsonAsync<AdminProductDto>($"/api/admin/products/{_productId}");

        Assert.NotNull(product);
        Assert.Equal(["gifts", "test-category"], product.CategorySlugs);
        Assert.Equal(["desk"], product.CollectionSlugs);

        // The single-valued field the list screen and the breadcrumb read is
        // the first of them, not something separate.
        Assert.Equal("gifts", product.CategorySlug);
    }

    [Fact]
    public async Task A_new_product_is_filed_under_every_category_it_was_created_with()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            new
            {
                title = "دفتر هدیه",
                brand = "test-brand",
                categories = new[] { "gifts", "test-category" },
                collections = new[] { "gifting" },
                price = 90_000,
                sku = "BZ-GIFT-01",
            });

        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<SavedIdResponse>();
        Assert.NotNull(created);
        var createdId = Guid.Parse(created.Id);

        Assert.Equal(["gifts", "test-category"], await FilingsAsync(createdId));
        Assert.Equal(["gifting"], await MembershipsAsync(createdId));
    }

    /// <summary>The categories the product is filed under, in stored order.</summary>
    private Task<List<string>> FilingsAsync(Guid? productId = null)
    {
        var id = productId ?? _productId;
        return _factory.WithDbAsync(async db => await db.ProductCategories.AsNoTracking()
            .Where(filing => filing.ProductId == id)
            .OrderBy(filing => filing.SortOrder)
            .Join(db.Categories.AsNoTracking(), filing => filing.CategoryId, c => c.Id, (_, c) => c.Slug)
            .ToListAsync());
    }

    private Task<List<string>> MembershipsAsync(Guid? productId = null)
    {
        var id = productId ?? _productId;
        return _factory.WithDbAsync(async db => await db.CollectionProducts.AsNoTracking()
            .Where(membership => membership.ProductId == id)
            .Join(db.Collections.AsNoTracking(), m => m.CollectionId, c => c.Id, (_, c) => c.Slug)
            .OrderBy(slug => slug)
            .ToListAsync());
    }

    private Task<HttpResponseMessage> SaveAsync(IReadOnlyList<string> categories) =>
        _client.PostAsJsonAsync(
            "/api/admin/products",
            new { id = _productId.ToString(), categories });

    private async Task<Paged<ProductDto>> BrowseAsync(string categorySlug)
    {
        using var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<Paged<ProductDto>>($"/api/products?category={categorySlug}");
        Assert.NotNull(page);
        return page;
    }

    private sealed record SavedIdResponse(string Id);
}
