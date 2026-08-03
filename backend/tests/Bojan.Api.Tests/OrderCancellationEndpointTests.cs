using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Application.Orders;
using Bojan.Domain.Admin;
using Bojan.Domain.Common;
using Bojan.Domain.Inventory;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Cancelling an order — <c>POST /api/admin/orders/cancel</c>.
/// </summary>
/// <remarks>
/// The rules themselves are covered against the domain in
/// <c>OrderCancellationTests</c>. What these hold is that the endpoint actually
/// performs them: that the stock lands back on the right counter, that the
/// wallet is credited once and only once, and that a movement row explains the
/// change to whoever reads the inventory screen afterwards.
/// </remarks>
public sealed class OrderCancellationEndpointTests : IAsyncLifetime, IDisposable
{
    private const int Stock = 5;
    private const int Quantity = 2;
    private const long UnitPrice = 300_000;
    private const long Shipping = 45_000;
    private const long Total = (UnitPrice * Quantity) + Shipping;

    private readonly BojanApiFactory _factory = new();
    private HttpClient _customer = null!;
    private HttpClient _owner = null!;
    private Guid _customerId;
    private Guid _productId;
    private Guid _addressId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", UnitPrice, Stock);
            var customer = await TestData.AddCustomerAsync(db, "09121110077");
            var address = await TestData.AddAddressAsync(db, customer.Id);
            await TestData.AddCheckoutMethodsAsync(db);

            // Paid from the balance, so there is something to refund. The
            // gateway's share is a separate question these do not touch.
            db.PaymentMethods.Add(new PaymentMethod { Code = "wallet", Title = "کیف پول", UsesWallet = true });
            customer.CreditWallet(new Money(Total));

            var admin = await TestData.AddAdminAsync(db, AdminRole.Owner, "cancel-owner@bojan.test");
            await db.SaveChangesAsync();

            _productId = product.Id;
            _customerId = customer.Id;
            _addressId = address.Id;
            _customer = _factory.CreateCustomerClient(customer.Id);
            _owner = _factory.CreateAdminClient(admin.Id);
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _customer?.Dispose();
        _owner?.Dispose();
        _factory.Dispose();
    }

    /// <summary>Places a wallet-paid order and returns its id.</summary>
    private async Task<Guid> PlaceOrderAsync()
    {
        var response = await _customer.PostAsJsonAsync("/api/orders", new
        {
            lines = new[] { new { productId = _productId.ToString(), quantity = Quantity } },
            addressId = _addressId.ToString(),
            shippingMethodId = "standard",
            paymentMethodId = "wallet",
        });

        response.EnsureSuccessStatusCode();

        Guid id = default;
        await _factory.WithDbAsync(async db => id = (await db.Orders.SingleAsync()).Id);
        return id;
    }

    private async Task SetPenaltyAsync(decimal percent) =>
        await _factory.WithDbAsync(async db =>
        {
            db.Settings.Add(new SettingEntry
            {
                Section = OrderCancellationService.PenaltySection,
                Key = OrderCancellationService.PenaltyKey,
                Value = percent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedById = Guid.NewGuid(),
            });
            await db.SaveChangesAsync();
        });

    private async Task MoveToAsync(Guid orderId, string status)
    {
        var response = await _owner.PostAsJsonAsync(
            "/api/admin/orders/status", new { id = orderId.ToString(), status });
        response.EnsureSuccessStatusCode();
    }

    private Task<HttpResponseMessage> CancelAsync(Guid orderId, bool chargePenalty = true) =>
        _owner.PostAsJsonAsync(
            "/api/admin/orders/cancel",
            new { id = orderId.ToString(), reason = "درخواست مشتری", chargePenalty });

    /// <summary>
    /// Before the warehouse there is nothing to charge for, and the goods never
    /// left — so the balance comes back whole and the shelf count is restored.
    /// </summary>
    [Fact]
    public async Task Cancelling_a_confirmed_order_refunds_in_full_and_puts_the_stock_back()
    {
        await SetPenaltyAsync(10m);
        var orderId = await PlaceOrderAsync();
        await MoveToAsync(orderId, "processing");

        var response = await CancelAsync(orderId);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Total, body.GetProperty("refunded").GetInt64());
        Assert.Equal(0, body.GetProperty("penalty").GetInt64());
        Assert.True(body.GetProperty("restocked").GetBoolean());

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(Total, (await db.Customers.SingleAsync(c => c.Id == _customerId)).WalletBalance.Amount);
            Assert.Equal(Stock, (await db.Products.SingleAsync(p => p.Id == _productId)).Stock);
            Assert.Equal(OrderStatus.Cancelled, (await db.Orders.SingleAsync()).Status);
        });
    }

    /// <summary>
    /// Once it has been picked and packed, that work is real and does not come
    /// back with the goods — which do come back, because they never shipped.
    /// </summary>
    [Fact]
    public async Task Cancelling_from_the_warehouse_withholds_the_penalty_but_still_restocks()
    {
        await SetPenaltyAsync(10m);
        var orderId = await PlaceOrderAsync();
        await MoveToAsync(orderId, "packed");

        var response = await CancelAsync(orderId);
        response.EnsureSuccessStatusCode();

        var expectedPenalty = Total / 10;

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedPenalty, body.GetProperty("penalty").GetInt64());
        Assert.Equal(Total - expectedPenalty, body.GetProperty("refunded").GetInt64());
        Assert.True(body.GetProperty("restocked").GetBoolean());

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(
                Total - expectedPenalty,
                (await db.Customers.SingleAsync(c => c.Id == _customerId)).WalletBalance.Amount);
            Assert.Equal(Stock, (await db.Products.SingleAsync(p => p.Id == _productId)).Stock);
        });
    }

    /// <summary>
    /// A dispatched parcel is with a carrier. It may come back, it may come back
    /// damaged, or not at all — so the count is left for an operator to correct
    /// once it is physically on the shelf.
    /// </summary>
    [Fact]
    public async Task Cancelling_after_dispatch_refunds_but_leaves_the_stock_to_an_operator()
    {
        await SetPenaltyAsync(20m);
        var orderId = await PlaceOrderAsync();
        await MoveToAsync(orderId, "shipped");

        var response = await CancelAsync(orderId);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("restocked").GetBoolean());

        await _factory.WithDbAsync(async db =>
        {
            // Still the reduced count: two units are out with the courier.
            Assert.Equal(Stock - Quantity, (await db.Products.SingleAsync(p => p.Id == _productId)).Stock);
            Assert.False(await db.StockMovements.AnyAsync());
        });
    }

    /// <summary>The restock is explained on the inventory screen, not left as an unattributed jump.</summary>
    [Fact]
    public async Task A_restock_records_why_the_count_moved()
    {
        var orderId = await PlaceOrderAsync();
        await MoveToAsync(orderId, "processing");
        (await CancelAsync(orderId)).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var movement = await db.StockMovements.SingleAsync();
            var number = (await db.Orders.SingleAsync()).Number;

            Assert.Equal(_productId, movement.ProductId);
            Assert.Equal(StockMovementKind.In, movement.Kind);
            Assert.Equal(Quantity, movement.Quantity);
            Assert.Contains(number, movement.Reason, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// The guard that stops a double-clicked cancel paying the refund twice —
    /// the same property the wallet top-up decision needed, and the reason the
    /// order row is locked before its status is read.
    /// </summary>
    [Fact]
    public async Task Cancelling_twice_refunds_and_restocks_once()
    {
        await SetPenaltyAsync(10m);
        var orderId = await PlaceOrderAsync();
        await MoveToAsync(orderId, "processing");

        (await CancelAsync(orderId)).EnsureSuccessStatusCode();

        var second = await CancelAsync(orderId);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(Total, (await db.Customers.SingleAsync(c => c.Id == _customerId)).WalletBalance.Amount);
            Assert.Equal(Stock, (await db.Products.SingleAsync(p => p.Id == _productId)).Stock);
            Assert.Equal(1, await db.StockMovements.CountAsync());
        });
    }

    /// <summary>A delivered order is returned rather than cancelled.</summary>
    [Fact]
    public async Task A_delivered_order_cannot_be_cancelled()
    {
        var orderId = await PlaceOrderAsync();
        await MoveToAsync(orderId, "delivered");

        var response = await CancelAsync(orderId);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(0, (await db.Customers.SingleAsync(c => c.Id == _customerId)).WalletBalance.Amount));
    }

    /// <summary>
    /// The shop cancelling is not the customer's doing, so the percentage does
    /// not apply however far along the order was.
    /// </summary>
    [Fact]
    public async Task A_cancellation_the_shop_made_charges_no_penalty()
    {
        await SetPenaltyAsync(25m);
        var orderId = await PlaceOrderAsync();
        await MoveToAsync(orderId, "packed");

        var response = await CancelAsync(orderId, chargePenalty: false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("penalty").GetInt64());
        Assert.Equal(Total, body.GetProperty("refunded").GetInt64());
    }

    /// <summary>The refund is a line on the wallet screen, so the deduction is visible rather than inferred.</summary>
    [Fact]
    public async Task The_refund_appears_on_the_wallet_ledger()
    {
        await SetPenaltyAsync(10m);
        var orderId = await PlaceOrderAsync();
        await MoveToAsync(orderId, "packed");
        (await CancelAsync(orderId)).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            // Paying from the balance wrote its own debit row, so the refund is
            // the credit beside it rather than the only entry.
            var entry = await db.WalletTransactions
                .SingleAsync(t => t.CustomerId == _customerId && t.Amount > 0);

            Assert.Equal(Total - (Total / 10), entry.Amount);
            Assert.Contains("جریمه", entry.Title, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// The shopper cancelling their own order, through the same implementation
    /// the panel uses — the penalty applies because by definition they asked.
    /// </summary>
    [Fact]
    public async Task A_customer_can_cancel_their_own_order()
    {
        await SetPenaltyAsync(10m);
        var orderId = await PlaceOrderAsync();
        await MoveToAsync(orderId, "packed");

        var response = await _customer.PostAsJsonAsync(
            "/api/me/orders/cancel", new { orderId = orderId.ToString() });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Total / 10, body.GetProperty("penalty").GetInt64());

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(OrderStatus.Cancelled, (await db.Orders.SingleAsync()).Status);
            Assert.Equal(Stock, (await db.Products.SingleAsync(p => p.Id == _productId)).Stock);
        });
    }

    /// <summary>
    /// Someone else's order is not found rather than forbidden — an order that
    /// exists must not be distinguishable from one that does not.
    /// </summary>
    [Fact]
    public async Task A_customer_cannot_cancel_someone_elses_order()
    {
        var orderId = await PlaceOrderAsync();

        Guid strangerId = default;
        await _factory.WithDbAsync(async db =>
            strangerId = (await TestData.AddCustomerAsync(db, "09121110099")).Id);

        using var stranger = _factory.CreateCustomerClient(strangerId);
        var response = await stranger.PostAsJsonAsync(
            "/api/me/orders/cancel", new { orderId = orderId.ToString() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync()).Status));
    }

    /// <summary>
    /// Cancelling moves money and stock, so it is not reachable as one more
    /// value of the status control — the panel routes it elsewhere and the
    /// server refuses it here for a request that never went near the panel.
    /// </summary>
    [Fact]
    public async Task The_status_endpoint_refuses_to_cancel()
    {
        var orderId = await PlaceOrderAsync();

        var response = await _owner.PostAsJsonAsync(
            "/api/admin/orders/status", new { id = orderId.ToString(), status = "cancelled" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync()).Status);
            Assert.Equal(0, (await db.Customers.SingleAsync(c => c.Id == _customerId)).WalletBalance.Amount);
        });
    }

    /// <summary>Working the order queue is not the same as being allowed to move money out of it.</summary>
    [Fact]
    public async Task An_operator_outside_the_order_roles_cannot_cancel()
    {
        var orderId = await PlaceOrderAsync();

        Guid productOnlyId = default;
        await _factory.WithDbAsync(async db =>
            productOnlyId = (await TestData.AddAdminAsync(db, AdminRole.Product, "product@bojan.test")).Id);

        using var productOnly = _factory.CreateAdminClient(productOnlyId);
        var response = await productOnly.PostAsJsonAsync(
            "/api/admin/orders/cancel", new { id = orderId.ToString(), chargePenalty = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(0, (await db.Customers.SingleAsync(c => c.Id == _customerId)).WalletBalance.Amount));
    }
}
