using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bojan.Api.Tests;

/// <summary>
/// Phase 2 — the catalogue reads, checked against the DTO shape
/// <c>apps/storefront/src/lib/api/types.ts</c> declares.
/// </summary>
/// <remarks>
/// The field-name assertions are not ceremony: the frontend does not validate
/// shapes, it reads properties. A renamed field is a blank screen, not an
/// error, so the names are asserted here where a rename fails loudly.
/// </remarks>
public sealed class CatalogueEndpointsTests : IDisposable
{
    private readonly BojanApiFactory _factory;
    private readonly HttpClient _client;

    public CatalogueEndpointsTests()
    {
        _factory = new BojanApiFactory();
        _factory.EnsureDatabaseCreated();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>
    /// A listing card has no variant picker, so it has to be told when a
    /// product cannot be bought without using one.
    /// </summary>
    /// <remarks>
    /// Without this the card's quick-add put the plain product in the basket:
    /// a shopper browsing a grid of brushes added «a brush», with no size on
    /// the line and the stock taken off the parent rather than off the one they
    /// meant. `IsActive` is part of it because that is what `ListSkusAsync`
    /// filters on — a product whose combinations are all switched off has none
    /// the product page would offer either, so the card may sell it plainly.
    /// </remarks>
    [Fact]
    public async Task A_product_says_whether_it_is_sold_by_combination()
    {
        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);

            var plain = await TestData.AddProductAsync(db, brandId, categoryId, "p-plain", 100_000, stock: 4);
            var sized = await TestData.AddProductAsync(db, brandId, categoryId, "p-sized", 100_000, stock: 4);
            var retired = await TestData.AddProductAsync(db, brandId, categoryId, "p-retired", 100_000, stock: 4);

            _ = plain;
            // A SKU code is unique across the whole shop, not per product.
            await TestData.AddSkuAsync(db, sized.Id, "sized-s1", 120_000, stock: 3);
            await TestData.AddSkuAsync(db, retired.Id, "retired-s1", 120_000, stock: 3, active: false);
        });

        var response = await _client.GetAsync("/api/products?pageSize=50");
        response.EnsureSuccessStatusCode();

        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

        bool Flag(string slug) => items.EnumerateArray()
            .First(item => item.GetProperty("slug").GetString() == slug)
            .GetProperty("hasVariants")
            .GetBoolean();

        Assert.False(Flag("p-plain"));
        Assert.True(Flag("p-sized"));
        // Every combination switched off is no combination to choose from.
        Assert.False(Flag("p-retired"));
    }

    [Fact]
    public async Task Product_listing_returns_the_paged_envelope_the_frontend_expects()
    {
        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 1_200_000, stock: 4);
        });

        var response = await _client.GetAsync("/api/products");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Paged<T> is { items, total, page, pageSize } — all four, or the
        // listing's pager silently renders nothing.
        Assert.Equal(1, body.GetProperty("total").GetInt32());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(24, body.GetProperty("pageSize").GetInt32());

        var product = body.GetProperty("items")[0];
        Assert.Equal("p-01", product.GetProperty("slug").GetString());
        Assert.Equal(1_200_000, product.GetProperty("price").GetInt64());
        Assert.Equal("برند آزمایشی", product.GetProperty("brand").GetString());
        Assert.Equal("test-category", product.GetProperty("categorySlug").GetString());
    }

    [Fact]
    public async Task An_optional_field_with_no_value_is_omitted_rather_than_null()
    {
        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            await TestData.AddProductAsync(db, brandId, categoryId, "p-plain", 500_000, stock: 1);
        });

        var body = await (await _client.GetAsync("/api/products/p-plain")).Content.ReadFromJsonAsync<JsonElement>();

        // The TypeScript declares `compareAtPrice?: number`, so absent is
        // `undefined`. Writing null would still be falsy today, but it is not
        // what the contract says.
        Assert.False(body.TryGetProperty("compareAtPrice", out _));
        Assert.False(body.TryGetProperty("description", out _));
    }

    [Fact]
    public async Task An_unknown_product_is_a_real_404()
    {
        var response = await _client.GetAsync("/api/products/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_unpublished_product_is_invisible_to_the_storefront()
    {
        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            await TestData.AddProductAsync(db, brandId, categoryId, "p-draft", 100_000, stock: 5, published: false);
        });

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/products/p-draft")).StatusCode);

        var listing = await (await _client.GetAsync("/api/products")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, listing.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Price_filters_and_sorting_run_in_the_database()
    {
        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            await TestData.AddProductAsync(db, brandId, categoryId, "cheap", 100_000, stock: 1);
            await TestData.AddProductAsync(db, brandId, categoryId, "mid", 500_000, stock: 1);
            await TestData.AddProductAsync(db, brandId, categoryId, "dear", 900_000, stock: 1);
        });

        var filtered = await (await _client.GetAsync("/api/products?minPrice=200000&maxPrice=800000"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, filtered.GetProperty("total").GetInt32());
        Assert.Equal("mid", filtered.GetProperty("items")[0].GetProperty("slug").GetString());

        var sorted = await (await _client.GetAsync("/api/products?sort=price-desc"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("dear", sorted.GetProperty("items")[0].GetProperty("slug").GetString());
    }

    [Fact]
    public async Task In_stock_only_drops_the_sold_out()
    {
        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            await TestData.AddProductAsync(db, brandId, categoryId, "available", 100_000, stock: 3);
            await TestData.AddProductAsync(db, brandId, categoryId, "sold-out", 100_000, stock: 0);
        });

        var body = await (await _client.GetAsync("/api/products?inStockOnly=true"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("total").GetInt32());
        Assert.Equal("available", body.GetProperty("items")[0].GetProperty("slug").GetString());
    }

    [Fact]
    public async Task Compare_returns_products_in_the_order_they_were_asked_for()
    {
        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            await TestData.AddProductAsync(db, brandId, categoryId, "a", 100_000, stock: 1);
            await TestData.AddProductAsync(db, brandId, categoryId, "b", 200_000, stock: 1);
        });

        var body = await (await _client.GetAsync("/api/products/compare?slugs=b,a,unknown"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, body.GetArrayLength());
        Assert.Equal("b", body[0].GetProperty("slug").GetString());
        Assert.Equal("a", body[1].GetProperty("slug").GetString());
    }

    [Fact]
    public async Task A_parent_category_lists_its_children_products_too()
    {
        await _factory.WithDbAsync(async db =>
        {
            var (brandId, _) = await TestData.AddCatalogueAsync(db);

            var parent = new Domain.Catalogue.Category { Slug = "stationery", Name = "نوشت‌افزار", Icon = "edit" };
            db.Categories.Add(parent);
            await db.SaveChangesAsync();

            var child = new Domain.Catalogue.Category
            {
                Slug = "pens",
                Name = "خودکار",
                Icon = "edit",
                ParentId = parent.Id,
            };
            db.Categories.Add(child);
            await db.SaveChangesAsync();

            await TestData.AddProductAsync(db, brandId, child.Id, "a-pen", 50_000, stock: 2);
        });

        var body = await (await _client.GetAsync("/api/products?category=stationery"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Shipping_methods_are_keyed_by_the_code_the_checkout_submits()
    {
        await _factory.WithDbAsync(TestData.AddCheckoutMethodsAsync);

        var body = await (await _client.GetAsync("/api/shipping-methods")).Content.ReadFromJsonAsync<JsonElement>();

        // The checkout validates its own shippingMethodId against the fixture's
        // ids, so the wire id has to be "standard" and not a GUID.
        Assert.Equal("standard", body[0].GetProperty("id").GetString());
        Assert.Equal(45_000, body[0].GetProperty("price").GetInt64());
    }
}
