using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Common;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The operator's half of the payment state, end to end.
/// </summary>
/// <remarks>
/// Adapted from Phonix, whose orders wait at <c>PendingApproval</c> until
/// someone confirms the receipt. The settlement is owner-only and decided under
/// the order's row lock, like the wallet top-up queue beside it.
/// </remarks>
public sealed class OrderPaymentSettlementTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _owner;
    private Guid _support;
    private Guid _orderId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            _owner = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@settle.test")).Id;
            _support = (await TestData.AddAdminAsync(db, AdminRole.Support, "support@settle.test")).Id;

            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-settle", 250_000, stock: 5);
            var customer = await TestData.AddCustomerAsync(db, "09121112233");
            var address = await TestData.AddAddressAsync(db, customer.Id);

            var order = Order.Create(
                OrderNumber.NewOrderNumber(),
                customer.Id,
                [new OrderLineDraft(product.Id, product.Slug, product.Title, product.ImageUrl, 1, product.Price)],
                address.Id,
                "تهران",
                "پست پیشتاز",
                "پرداخت اینترنتی",
                "gateway",
                product.Price,
                Money.Zero,
                Money.Zero,
                Guid.NewGuid().ToString());

            db.Orders.Add(order);
            await db.SaveChangesAsync();
            _orderId = order.Id;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> SettleAsync(Guid actor, bool paid = true, string? reference = "TRK-1")
    {
        var client = _factory.CreateAdminClient(actor);
        return client.PostAsJsonAsync(
            "/api/admin/orders/payment", new { id = _orderId.ToString(), paid, reference });
    }

    private Task<HttpResponseMessage> MoveAsync(Guid actor, string status)
    {
        var client = _factory.CreateAdminClient(actor);
        return client.PostAsJsonAsync(
            "/api/admin/orders/status", new { id = _orderId.ToString(), status });
    }

    private async Task<Order> ReadOrderAsync()
    {
        Order? order = null;
        await _factory.WithDbAsync(async db =>
            order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == _orderId));
        return order!;
    }

    [Fact]
    public async Task An_order_is_placed_awaiting_payment()
    {
        Assert.Equal(OrderPaymentStatus.AwaitingPayment, (await ReadOrderAsync()).PaymentStatus);
    }

    [Fact]
    public async Task An_owner_settles_it_and_the_operator_is_recorded()
    {
        (await SettleAsync(_owner)).EnsureSuccessStatusCode();

        var order = await ReadOrderAsync();
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal("TRK-1", order.PaymentReference);
        Assert.Equal(_owner, order.SettledById);
        Assert.NotNull(order.PaidAtUtc);
    }

    [Fact]
    public async Task A_support_operator_cannot_settle_a_payment()
    {
        // It hands goods out of the building. Owner only, like the wallet queue.
        Assert.Equal(HttpStatusCode.Forbidden, (await SettleAsync(_support)).StatusCode);
        Assert.Equal(OrderPaymentStatus.AwaitingPayment, (await ReadOrderAsync()).PaymentStatus);
    }

    [Fact]
    public async Task Settling_twice_is_refused_rather_than_repeated()
    {
        (await SettleAsync(_owner, reference: "first")).EnsureSuccessStatusCode();

        var again = await SettleAsync(_owner, reference: "second");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("first", (await ReadOrderAsync()).PaymentReference);
    }

    [Fact]
    public async Task An_unpaid_order_cannot_be_marked_shipped()
    {
        (await MoveAsync(_owner, "processing")).EnsureSuccessStatusCode();
        (await MoveAsync(_owner, "packed")).EnsureSuccessStatusCode();

        var shipped = await MoveAsync(_owner, "shipped");

        Assert.Equal(HttpStatusCode.Conflict, shipped.StatusCode);
        Assert.Contains("order-not-paid", await shipped.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Settling_the_payment_releases_it_to_ship()
    {
        (await MoveAsync(_owner, "processing")).EnsureSuccessStatusCode();
        (await MoveAsync(_owner, "packed")).EnsureSuccessStatusCode();
        (await SettleAsync(_owner)).EnsureSuccessStatusCode();

        (await MoveAsync(_owner, "shipped")).EnsureSuccessStatusCode();

        Assert.Equal(OrderStatus.Shipped, (await ReadOrderAsync()).Status);
    }

    [Fact]
    public async Task Settling_tells_the_customer()
    {
        (await SettleAsync(_owner)).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == _orderId);
            var notified = await db.CustomerNotifications.AsNoTracking()
                .AnyAsync(n => n.CustomerId == order.CustomerId && n.Body.Contains(order.Number));

            Assert.True(notified);
        });
    }

    [Fact]
    public async Task A_transition_records_where_it_came_from_and_who_moved_it()
    {
        (await MoveAsync(_owner, "processing")).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var entry = await db.OrderTimelineEvents.AsNoTracking()
                .Where(e => e.OrderId == _orderId && e.Status == OrderStatus.Processing)
                .SingleAsync();

            Assert.Equal(OrderStatus.Pending, entry.FromStatus);
            Assert.Equal(_owner, entry.ActorId);
        });
    }
}
