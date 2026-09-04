using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Nothing sellable is priced at nothing, and a discount belongs to one
/// combination.
/// </summary>
/// <remarks>
/// <para>
/// A product with variants is not priced by its own <c>Price</c>: a shopper who
/// picks size 4 pays what size 4 costs, and the checkout charges the SKU. That
/// makes each combination a thing that has to be priced in its own right, and a
/// combination left at zero is one nobody filled in — published, it gives the
/// product away.
/// </para>
/// <para>
/// The single exception is deliberate: zero under a list price is a
/// hundred-percent discount, which somebody chose. That is why the rule reads
/// the pair rather than the price alone.
/// </para>
/// <para>
/// The discount lives on the SKU rather than on the product so that reducing
/// one size cannot reduce another — the case the storefront used to get wrong
/// by striking every size through against the product's own list price.
/// </para>
/// </remarks>
public sealed class VariantPricingTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _productId;
    private Guid _brandId;
    private Guid _categoryId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;

        await _factory.WithDbAsync(async db =>
        {
            (_brandId, _categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, _brandId, _categoryId, "brush", 100_000, stock: 20);
            _productId = product.Id;

            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "pricing-owner@bojan.test")).Id;
        });

        _client = _factory.CreateAdminClient(ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    // --- a combination has to be priced ------------------------------------

    [Fact]
    public async Task A_combination_priced_at_nothing_is_refused()
    {
        var response = await SaveSkusAsync(
            new { code = "BR-S2", combination = "s2", price = 0, stock = 3, active = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("price-required", await DetailAsync(response));
    }

    /// <summary>
    /// The one way to sell something for nothing, and it takes saying what it
    /// used to cost.
    /// </summary>
    [Fact]
    public async Task Zero_under_a_list_price_is_a_hundred_percent_discount()
    {
        (await SaveSkusAsync(
            new { code = "BR-S2", combination = "s2", price = 0, stock = 3, active = true, compareAt = 90_000 }))
            .EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var sku = await db.ProductSkus.SingleAsync(s => s.Code == "BR-S2");
            Assert.Equal(0, sku.Price.Amount);
            Assert.Equal(90_000, sku.CompareAtPrice!.Value.Amount);
            Assert.True(sku.IsSellable);
        });
    }

    [Fact]
    public async Task A_list_price_at_or_below_the_selling_price_is_refused()
    {
        var response = await SaveSkusAsync(
            new { code = "BR-S2", combination = "s2", price = 2_000, stock = 3, active = true, compareAt = 1_500 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("compare-at-too-low", await DetailAsync(response));
    }

    // --- a discount belongs to one combination -----------------------------

    [Fact]
    public async Task Discounting_one_size_leaves_the_others_at_their_own_price()
    {
        (await SaveSkusAsync(
            new { code = "BR-S2", combination = "s2", price = 800, stock = 5, active = true, compareAt = 1_000 },
            new { code = "BR-S4", combination = "s4", price = 2_000, stock = 4, active = true }))
            .EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var reduced = await db.ProductSkus.SingleAsync(s => s.Code == "BR-S2");
            var untouched = await db.ProductSkus.SingleAsync(s => s.Code == "BR-S4");

            Assert.Equal(800, reduced.Price.Amount);
            Assert.Equal(1_000, reduced.CompareAtPrice!.Value.Amount);

            // The point of the test: the other size is not on sale at all.
            Assert.Equal(2_000, untouched.Price.Amount);
            Assert.Null(untouched.CompareAtPrice);
        });
    }

    /// <summary>
    /// The storefront reads the list price per combination, so a page can strike
    /// through the size the shopper picked and no other.
    /// </summary>
    [Fact]
    public async Task The_storefront_is_told_which_size_is_on_sale()
    {
        (await SaveSkusAsync(
            new { code = "BR-S2", combination = "s2", price = 800, stock = 5, active = true, compareAt = 1_000 },
            new { code = "BR-S4", combination = "s4", price = 2_000, stock = 4, active = true }))
            .EnsureSuccessStatusCode();

        var skus = await _client.GetFromJsonAsync<List<StorefrontSku>>("/api/products/brush/skus");

        Assert.NotNull(skus);
        Assert.Equal(1_000, skus.Single(s => s.Combination == "s2").CompareAt);
        Assert.Null(skus.Single(s => s.Combination == "s4").CompareAt);
    }

    [Fact]
    public async Task Clearing_a_list_price_takes_the_combination_off_sale()
    {
        (await SaveSkusAsync(
            new { code = "BR-S2", combination = "s2", price = 800, stock = 5, active = true, compareAt = 1_000 }))
            .EnsureSuccessStatusCode();

        (await SaveSkusAsync(
            new { code = "BR-S2", combination = "s2", price = 800, stock = 5, active = true, compareAt = 0 }))
            .EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.Null((await db.ProductSkus.SingleAsync(s => s.Code == "BR-S2")).CompareAtPrice));
    }

    // --- the product itself ------------------------------------------------

    [Fact]
    public async Task A_product_priced_at_nothing_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products", new
        {
            title = "محصول بی‌قیمت",
            sku = "BZ-FREE-01",
            brand = "test-brand",
            categories = new[] { "test-category" },
            price = 0,
            stock = 5,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("price-required", await DetailAsync(response));
    }

    /// <summary>
    /// A save that names no price leaves the stored one alone, so editing a
    /// product's title must not be read as pricing it at nothing.
    /// </summary>
    [Fact]
    public async Task A_save_that_names_no_price_keeps_the_one_the_product_had()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products", new
        {
            id = _productId.ToString(),
            title = "قلم‌موی تازه",
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.Equal(100_000, (await db.Products.SingleAsync(p => p.Id == _productId)).Price.Amount));
    }

    // --- the two prices stay separate ---------------------------------------

    /// <summary>
    /// Pricing a product and pricing its combinations are two decisions, and
    /// one must not overwrite the other.
    /// </summary>
    /// <remarks>
    /// This used to copy the cheapest combination into the product's own price
    /// on every SKU save, which destroyed the figure set on the pricing screen
    /// and left it wrong the moment the combinations changed.
    /// </remarks>
    [Fact]
    public async Task Pricing_the_sizes_leaves_the_products_own_price_alone()
    {
        (await SaveSkusAsync(
            new { code = "BR-S1", combination = "s1", price = 10_000, stock = 5, active = true },
            new { code = "BR-S2", combination = "s2", price = 11_000, stock = 4, active = true }))
            .EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.Equal(100_000, (await db.Products.SingleAsync(p => p.Id == _productId)).Price.Amount));
    }

    /// <summary>
    /// The shop window still shows a figure a shopper could actually pay: the
    /// cheapest combination, computed on the way out rather than stored.
    /// </summary>
    [Fact]
    public async Task The_card_shows_the_cheapest_combination()
    {
        (await SaveSkusAsync(
            new { code = "BR-S2", combination = "s2", price = 11_000, stock = 4, active = true },
            new { code = "BR-S1", combination = "s1", price = 10_000, stock = 5, active = true }))
            .EnsureSuccessStatusCode();

        var product = await _client.GetFromJsonAsync<StorefrontProduct>("/api/products/brush");

        Assert.NotNull(product);
        Assert.Equal(10_000, product.Price);
        Assert.True(product.HasVariants);
    }

    /// <summary>
    /// And the strike-through belongs to that same combination, not to the
    /// product's own list price.
    /// </summary>
    [Fact]
    public async Task The_cards_list_price_belongs_to_the_cheapest_combination()
    {
        (await SaveSkusAsync(
            new { code = "BR-S1", combination = "s1", price = 8_000, stock = 5, active = true, compareAt = 12_000 },
            new { code = "BR-S2", combination = "s2", price = 11_000, stock = 4, active = true }))
            .EnsureSuccessStatusCode();

        var product = await _client.GetFromJsonAsync<StorefrontProduct>("/api/products/brush");

        Assert.NotNull(product);
        Assert.Equal(8_000, product.Price);
        Assert.Equal(12_000, product.CompareAtPrice);
    }

    /// <summary>
    /// A product with no combinations is priced by the pricing screen, which is
    /// the other half of the same rule.
    /// </summary>
    [Fact]
    public async Task Without_combinations_the_card_shows_the_products_own_price()
    {
        var product = await _client.GetFromJsonAsync<StorefrontProduct>("/api/products/brush");

        Assert.NotNull(product);
        Assert.Equal(100_000, product.Price);
        Assert.False(product.HasVariants);
    }

    /// <summary>
    /// Removing the combinations hands pricing back to the product's own figure,
    /// which is still the one the operator set.
    /// </summary>
    [Fact]
    public async Task Removing_every_combination_restores_the_products_own_price()
    {
        (await SaveSkusAsync(
            new { code = "BR-S1", combination = "s1", price = 10_000, stock = 5, active = true }))
            .EnsureSuccessStatusCode();

        (await SaveSkusAsync()).EnsureSuccessStatusCode();

        var product = await _client.GetFromJsonAsync<StorefrontProduct>("/api/products/brush");

        Assert.NotNull(product);
        Assert.Equal(100_000, product.Price);
    }

    private Task<HttpResponseMessage> SaveSkusAsync(params object[] skus) =>
        _client.PostAsJsonAsync("/api/admin/products/skus", new { id = _productId.ToString(), skus });

    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<Problem>();
        return problem?.Detail;
    }

    private sealed record Problem(string? Title, string? Detail);

    private sealed record StorefrontSku(string Combination, long Price, long? CompareAt);

    private sealed record StorefrontProduct(long Price, long? CompareAtPrice, bool HasVariants);
}
