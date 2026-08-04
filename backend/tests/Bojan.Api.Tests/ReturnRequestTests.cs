using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Common;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// <c>POST /me/returns</c> — filing a return against a delivered order.
/// </summary>
/// <remarks>
/// What the endpoint has to establish is that the order is the caller's own and
/// that it actually contains what is being sent back. Everything downstream — an
/// operator approving it, a refund being paid — takes the filed request at face
/// value, so a claim that gets past here is a claim someone will act on.
/// </remarks>
public sealed class ReturnRequestTests : IAsyncLifetime, IDisposable
{
    private const int OrderedQuantity = 5;

    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _customerId;
    private Guid _productId;
    private Guid _orderId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 300_000, stock: 20);
            var customer = await TestData.AddCustomerAsync(db, "09121110055");
            var address = await TestData.AddAddressAsync(db, customer.Id);

            var order = Order.Create(
                OrderNumber.NewOrderNumber(),
                customer.Id,
                [new OrderLineDraft(
                    product.Id, product.Slug, product.Title, product.ImageUrl,
                    OrderedQuantity, product.Price)],
                address.Id,
                "تهران، خیابان آزمون",
                "پست پیشتاز",
                "پرداخت در محل",
                subtotal: new Money(300_000 * OrderedQuantity),
                discount: Money.Zero,
                shipping: Money.Zero,
                idempotencyKey: "seed-return-tests");

            // Only a delivered or shipped order has anything to send back.
            order.TransitionTo(OrderStatus.Processing);
            order.TransitionTo(OrderStatus.Shipped);
            order.TransitionTo(OrderStatus.Delivered);

            db.Orders.Add(order);
            await db.SaveChangesAsync();

            _customerId = customer.Id;
            _productId = product.Id;
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

    private Task<HttpResponseMessage> FileAsync(params object[] items) =>
        _client.PostAsJsonAsync(
            "/api/me/returns",
            new { orderId = _orderId.ToString(), items, reason = "کالا آسیب دیده بود" });

    private object Item(int quantity) => new { productId = _productId.ToString(), quantity };

    [Fact]
    public async Task A_return_for_what_the_order_contains_is_accepted()
    {
        var response = await FileAsync(Item(2));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("quantity").GetInt32());

        await _factory.WithDbAsync(async db =>
            Assert.Equal(2, (await db.ReturnItems.SingleAsync()).Quantity));
    }

    [Fact]
    public async Task A_return_for_more_than_the_order_contains_is_refused()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await FileAsync(Item(OrderedQuantity + 1))).StatusCode);

        await _factory.WithDbAsync(async db => Assert.Empty(await db.ReturnRequests.ToListAsync()));
    }

    /// <summary>
    /// The same product twice is one claim for the sum, not two claims each
    /// judged alone.
    /// </summary>
    /// <remarks>
    /// The quantity rule is about the product, so checking each entry in
    /// isolation let a five-unit line accept two entries of five and file a
    /// return for ten — more than was ever bought, in front of an operator who
    /// sees a request the order appears to support.
    /// </remarks>
    [Fact]
    public async Task Repeating_a_product_cannot_return_more_of_it_than_was_bought()
    {
        var response = await FileAsync(Item(OrderedQuantity), Item(OrderedQuantity));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.Empty(await db.ReturnRequests.ToListAsync()));
    }

    /// <summary>Repeats that do stay within the line are summed into one item rather than duplicated.</summary>
    [Fact]
    public async Task Repeating_a_product_within_the_line_is_summed_into_one_item()
    {
        (await FileAsync(Item(1), Item(2))).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var item = await db.ReturnItems.SingleAsync();
            Assert.Equal(3, item.Quantity);
        });
    }

    /// <summary>A product the order never contained is not returnable against it.</summary>
    [Fact]
    public async Task A_product_the_order_does_not_contain_is_refused()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/me/returns",
            new
            {
                orderId = _orderId.ToString(),
                items = new[] { new { productId = Guid.NewGuid().ToString(), quantity = 1 } },
                reason = "کالا آسیب دیده بود",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Someone else's order is not found rather than forbidden — the lookup is
    /// scoped to the caller, so there is no other answer it could give.
    /// </summary>
    [Fact]
    public async Task Another_customers_order_cannot_be_returned()
    {
        Guid strangerId = default;
        await _factory.WithDbAsync(async db =>
            strangerId = (await TestData.AddCustomerAsync(db, "09121110056")).Id);

        using var stranger = _factory.CreateCustomerClient(strangerId);
        var response = await stranger.PostAsJsonAsync(
            "/api/me/returns",
            new
            {
                orderId = _orderId.ToString(),
                items = new[] { new { productId = _productId.ToString(), quantity = 1 } },
                reason = "کالا آسیب دیده بود",
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.Empty(await db.ReturnRequests.ToListAsync()));
    }
}
