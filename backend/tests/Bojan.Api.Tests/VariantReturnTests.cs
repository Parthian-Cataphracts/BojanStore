using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Common;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// A return names a combination, not just a product.
/// </summary>
/// <remarks>
/// An order can hold two lines of one product in different variants — a red one
/// and a blue one — and a return used to name only the product. Three things
/// followed. The order line was matched with <c>FirstOrDefault</c> on the
/// product, so the request attached itself to whichever line came first; the
/// already-claimed count was pooled across both, so returning the red exhausted
/// the blue's allowance too; and restocking credited the same wrongly-matched
/// line, which is how a variant that is out of stock ends up looking available.
/// </remarks>
public sealed class VariantReturnTests : IAsyncLifetime, IDisposable
{
    private const int PerVariant = 2;

    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _customerId;
    private Guid _productId;
    private Guid _redSkuId;
    private Guid _blueSkuId;
    private Guid _orderId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 300_000, stock: 20);

            var red = await TestData.AddSkuAsync(db, product.Id, "RED", 300_000, stock: 0);
            var blue = await TestData.AddSkuAsync(db, product.Id, "BLUE", 300_000, stock: 0);

            var customer = await TestData.AddCustomerAsync(db, "09121110056");
            var address = await TestData.AddAddressAsync(db, customer.Id);

            // One order, two lines, same product, different combinations.
            var order = Order.Create(
                OrderNumber.NewOrderNumber(),
                customer.Id,
                [
                    new OrderLineDraft(
                        product.Id, product.Slug, product.Title, product.ImageUrl,
                        PerVariant, product.Price, red.Id),
                    new OrderLineDraft(
                        product.Id, product.Slug, product.Title, product.ImageUrl,
                        PerVariant, product.Price, blue.Id),
                ],
                address.Id,
                "تهران، خیابان آزمون",
                "پست پیشتاز",
                "پرداخت در محل",
                "cod",
                subtotal: new Money(300_000 * PerVariant * 2),
                discount: Money.Zero,
                shipping: Money.Zero,
                idempotencyKey: "seed-variant-return-tests");

            order.TransitionTo(OrderStatus.Processing);
            order.TransitionTo(OrderStatus.Shipped);
            order.TransitionTo(OrderStatus.Delivered);

            db.Orders.Add(order);
            await db.SaveChangesAsync();

            _customerId = customer.Id;
            _productId = product.Id;
            _redSkuId = red.Id;
            _blueSkuId = blue.Id;
            _orderId = order.Id;
        });

        _client = _factory.CreateCustomerClient(_customerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> FileAsync(Guid skuId, int quantity) =>
        _client.PostAsJsonAsync("/api/me/returns", new
        {
            orderId = _orderId.ToString(),
            items = new[]
            {
                new { productId = _productId.ToString(), skuId = skuId.ToString(), quantity },
            },
            reason = "رنگ اشتباه بود",
        });

    /// <summary>
    /// The one that used to fail: both variants are returnable in full, because
    /// they are separate lines with separate allowances.
    /// </summary>
    [Fact]
    public async Task Returning_one_variant_does_not_spend_the_other_variants_allowance()
    {
        (await FileAsync(_redSkuId, PerVariant)).EnsureSuccessStatusCode();
        (await FileAsync(_blueSkuId, PerVariant)).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var items = await db.ReturnItems.AsNoTracking().ToListAsync();

            Assert.Equal(2, items.Count);
            Assert.Contains(items, item => item.SkuId == _redSkuId && item.Quantity == PerVariant);
            Assert.Contains(items, item => item.SkuId == _blueSkuId && item.Quantity == PerVariant);
        });
    }

    /// <summary>The per-line ceiling still holds — it is now per line rather than per product.</summary>
    [Fact]
    public async Task More_than_that_variant_was_bought_is_still_refused()
    {
        var response = await FileAsync(_redSkuId, PerVariant + 1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _factory.WithDbAsync(async db => Assert.Equal(0, await db.ReturnItems.CountAsync()));
    }

    [Fact]
    public async Task The_same_variant_twice_over_still_exhausts_its_own_allowance()
    {
        (await FileAsync(_redSkuId, PerVariant)).EnsureSuccessStatusCode();

        var second = await FileAsync(_redSkuId, 1);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    /// <summary>A combination the order never sold is not a line to attach to.</summary>
    [Fact]
    public async Task A_variant_the_order_does_not_contain_is_refused()
    {
        var response = await FileAsync(Guid.NewGuid(), 1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Restocking credits the variant that came back, not whichever line was
    /// found first.
    /// </summary>
    [Fact]
    public async Task Receiving_a_variant_puts_that_variants_stock_back()
    {
        (await FileAsync(_blueSkuId, PerVariant)).EnsureSuccessStatusCode();

        Guid requestId = default;
        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
        {
            requestId = (await db.ReturnRequests.SingleAsync()).Id;
            ownerId = (await TestData.AddAdminAsync(db, Domain.Admin.AdminRole.Owner, "owner@bojan.test")).Id;
        });

        using var owner = _factory.CreateAdminClient(ownerId);

        foreach (var status in new[] { "reviewing", "approved", "received" })
        {
            var moved = await owner.PostAsJsonAsync(
                "/api/admin/returns/decide",
                new { id = requestId.ToString(), status, restock = true });

            moved.EnsureSuccessStatusCode();
        }

        await _factory.WithDbAsync(async db =>
        {
            var blue = await db.ProductSkus.AsNoTracking().SingleAsync(sku => sku.Id == _blueSkuId);
            var red = await db.ProductSkus.AsNoTracking().SingleAsync(sku => sku.Id == _redSkuId);

            Assert.Equal(PerVariant, blue.Stock);
            Assert.Equal(0, red.Stock);
        });
    }
}
