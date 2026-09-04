using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// A SKU keeps its id across a save.
/// </summary>
/// <remarks>
/// <para>
/// Screen 108 posts the product's whole SKU list and the API replaces what it
/// holds. That used to be literal — every row deleted and written again — so a
/// save minted a new id for a combination that had not changed.
/// </para>
/// <para>
/// The id is not an implementation detail. A shopper's basket line names it, and
/// an order line records it to say which variant was sold. Re-minting it meant
/// that correcting one price emptied that variant out of every basket holding
/// it, and set <c>SkuId</c> to null on every order line that had referenced it,
/// so a past invoice could no longer say which size it was for.
/// </para>
/// <para>
/// This matters more now than it did: the variants screen asks operators to
/// price each size, which makes editing a SKU's price routine rather than rare.
/// </para>
/// </remarks>
public sealed class SkuIdentityTests : IAsyncLifetime, IDisposable
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
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "brush", 100_000, stock: 20);
            _productId = product.Id;

            db.ProductSkus.AddRange(
                Sku(product.Id, "BR-S2", "s2", 180_000, 12),
                Sku(product.Id, "BR-S4", "s4", 220_000, 8));

            await db.SaveChangesAsync();

            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "sku-owner@bojan.test")).Id;
        });

        _client = _factory.CreateAdminClient(ownerId);
    }

    private static ProductSku Sku(Guid productId, string code, string combination, long price, int stock)
    {
        var sku = new ProductSku
        {
            ProductId = productId,
            Code = code,
            Combination = combination,
            Price = new Money(price),
        };

        sku.SetStock(stock);
        return sku;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Repricing_a_size_leaves_every_id_where_it_was()
    {
        var before = await IdsByCodeAsync();

        (await SaveAsync(
            new { code = "BR-S2", combination = "s2", price = 195_000, stock = 12, active = true },
            new { code = "BR-S4", combination = "s4", price = 220_000, stock = 8, active = true }))
            .EnsureSuccessStatusCode();

        Assert.Equal(before, await IdsByCodeAsync());

        await _factory.WithDbAsync(async db =>
        {
            var repriced = await db.ProductSkus.SingleAsync(sku => sku.Code == "BR-S2");
            Assert.Equal(195_000, repriced.Price.Amount);
        });
    }

    [Fact]
    public async Task A_basket_holding_the_repriced_size_still_resolves_it()
    {
        var skuId = (await IdsByCodeAsync())["BR-S2"];

        (await SaveAsync(
            new { code = "BR-S2", combination = "s2", price = 195_000, stock = 12, active = true },
            new { code = "BR-S4", combination = "s4", price = 220_000, stock = 8, active = true }))
            .EnsureSuccessStatusCode();

        // The id the shopper is holding is still a SKU of this product, which is
        // the whole question the checkout asks before it prices the line.
        await _factory.WithDbAsync(async db =>
        {
            var held = await db.ProductSkus.SingleOrDefaultAsync(sku => sku.Id == skuId);
            Assert.NotNull(held);
            Assert.Equal(_productId, held.ProductId);
            Assert.Equal(195_000, held.Price.Amount);
        });
    }

    [Fact]
    public async Task A_code_left_off_the_list_is_removed()
    {
        (await SaveAsync(new { code = "BR-S2", combination = "s2", price = 180_000, stock = 12, active = true }))
            .EnsureSuccessStatusCode();

        Assert.Equal(["BR-S2"], (await IdsByCodeAsync()).Keys.Order());
    }

    [Fact]
    public async Task A_new_code_is_added_beside_the_ones_that_stay()
    {
        var before = await IdsByCodeAsync();

        (await SaveAsync(
            new { code = "BR-S2", combination = "s2", price = 180_000, stock = 12, active = true },
            new { code = "BR-S4", combination = "s4", price = 220_000, stock = 8, active = true },
            new { code = "BR-S6", combination = "s6", price = 260_000, stock = 4, active = true }))
            .EnsureSuccessStatusCode();

        var after = await IdsByCodeAsync();

        Assert.Equal(before["BR-S2"], after["BR-S2"]);
        Assert.Equal(before["BR-S4"], after["BR-S4"]);
        Assert.True(after.ContainsKey("BR-S6"));
    }

    /// <summary>
    /// Stock is set from the posted figure, not added to it — the screen shows
    /// a count and an operator typing 12 means twelve, not twelve more.
    /// </summary>
    [Fact]
    public async Task Stock_is_replaced_rather_than_accumulated()
    {
        (await SaveAsync(
            new { code = "BR-S2", combination = "s2", price = 180_000, stock = 3, active = true },
            new { code = "BR-S4", combination = "s4", price = 220_000, stock = 8, active = true }))
            .EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var sku = await db.ProductSkus.SingleAsync(candidate => candidate.Code == "BR-S2");
            Assert.Equal(3, sku.Stock);
        });
    }

    private Task<HttpResponseMessage> SaveAsync(params object[] skus) =>
        _client.PostAsJsonAsync("/api/admin/products/skus", new { id = _productId.ToString(), skus });

    private async Task<Dictionary<string, Guid>> IdsByCodeAsync()
    {
        Dictionary<string, Guid> map = [];

        await _factory.WithDbAsync(async db =>
        {
            map = await db.ProductSkus
                .Where(sku => sku.ProductId == _productId)
                .ToDictionaryAsync(sku => sku.Code, sku => sku.Id);
        });

        return map;
    }
}
