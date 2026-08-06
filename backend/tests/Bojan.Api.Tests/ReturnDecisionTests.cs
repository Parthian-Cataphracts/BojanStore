using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Common;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The operator's half of a return, end to end.
/// </summary>
/// <remarks>
/// Before this existed a filed return could not be moved at all —
/// <see cref="ReturnRequest.TransitionTo"/> had no callers, so every request
/// ever filed sat at <see cref="ReturnStatus.Submitted"/> and the customer's
/// tracker drew a first step that never advanced. What these check is the part
/// that costs money if it is wrong: that a refund is computed from the order
/// rather than taken from the request, that it is paid once, and that stock only
/// moves when someone says it should.
/// </remarks>
public sealed class ReturnDecisionTests : IAsyncLifetime, IDisposable
{
    private const long UnitPrice = 200_000;
    private const int Ordered = 4;
    private const int Returning = 2;
    private const int StartingStock = 10;

    private readonly BojanApiFactory _factory = new();
    private Guid _support;
    private Guid _productId;
    private Guid _customerId;
    private Guid _orderId;
    private Guid _returnId;

    /// <summary>A second order, cash on delivery and never reconciled — delivered but unpaid.</summary>
    private Guid _unpaidReturnId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            _support = (await TestData.AddAdminAsync(db, AdminRole.Support, "support@returns.test")).Id;

            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-ret", UnitPrice, StartingStock);
            var customer = await TestData.AddCustomerAsync(db, "09121114455");
            var address = await TestData.AddAddressAsync(db, customer.Id);

            _productId = product.Id;
            _customerId = customer.Id;

            var paid = Delivered(product, address.Id, customer.Id, "پرداخت اینترنتی", "gateway", settle: true);
            var unpaid = Delivered(product, address.Id, customer.Id, "پرداخت در محل", "cod", settle: false);

            db.Orders.Add(paid);
            db.Orders.Add(unpaid);

            var request = Filed(paid, product, customer.Id, "wallet");
            var againstUnpaid = Filed(unpaid, product, customer.Id, "wallet");

            db.ReturnRequests.Add(request);
            db.ReturnRequests.Add(againstUnpaid);
            await db.SaveChangesAsync();

            _orderId = paid.Id;
            _returnId = request.Id;
            _unpaidReturnId = againstUnpaid.Id;
        });
    }

    /// <summary>An order that reached the customer, settled or not.</summary>
    private static Order Delivered(
        Domain.Catalogue.Product product,
        Guid addressId,
        Guid customerId,
        string methodName,
        string methodCode,
        bool settle)
    {
        var order = Order.Create(
            OrderNumber.NewOrderNumber(),
            customerId,
            [new OrderLineDraft(product.Id, product.Slug, product.Title, product.ImageUrl, Ordered, product.Price)],
            addressId,
            "تهران",
            "پست پیشتاز",
            methodName,
            methodCode,
            subtotal: new Money(UnitPrice * Ordered),
            discount: Money.Zero,
            shipping: Money.Zero,
            idempotencyKey: Guid.NewGuid().ToString());

        if (settle)
        {
            // Nothing ships unpaid but cash on delivery, so the gateway order
            // has to be settled before it can reach the customer at all.
            order.MarkPaid(DateTimeOffset.UtcNow, "TRK-1", Guid.NewGuid());
        }

        order.TransitionTo(OrderStatus.Processing);
        order.TransitionTo(OrderStatus.Shipped);
        order.TransitionTo(OrderStatus.Delivered);
        return order;
    }

    private static ReturnRequest Filed(
        Order order,
        Domain.Catalogue.Product product,
        Guid customerId,
        string refundMethod)
    {
        var requestId = Guid.NewGuid();

        return ReturnRequest.Create(
            OrderNumber.NewReturnCode(),
            customerId,
            order.Id,
            order.Number,
            "کالا آسیب دیده بود",
            null,
            refundMethod,
            [new ReturnItem
            {
                ReturnRequestId = requestId,
                ProductId = product.Id,
                ProductSlug = product.Slug,
                ProductTitle = product.Title,
                ProductImageUrl = product.ImageUrl,
                Quantity = Returning,
            }],
            DateTimeOffset.UtcNow);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> DecideAsync(
        string status, Guid? id = null, bool restock = true, string? note = null)
    {
        var client = _factory.CreateAdminClient(_support);
        return client.PostAsJsonAsync(
            "/api/admin/returns/decide",
            new { id = (id ?? _returnId).ToString(), status, note, restock });
    }

    private async Task<ReturnRequest> ReadReturnAsync(Guid? id = null)
    {
        ReturnRequest? request = null;
        await _factory.WithDbAsync(async db =>
            request = await db.ReturnRequests.AsNoTracking().FirstAsync(r => r.Id == (id ?? _returnId)));
        return request!;
    }

    /// <summary>Walks the request to the step before the one under test.</summary>
    private async Task ReachAsync(params string[] steps)
    {
        foreach (var step in steps)
        {
            (await DecideAsync(step)).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task An_operator_moves_a_request_along_and_the_tracker_records_it()
    {
        (await DecideAsync("reviewing", note: "در حال بررسی")).EnsureSuccessStatusCode();

        var request = await ReadReturnAsync();
        Assert.Equal(ReturnStatus.Reviewing, request.Status);
        Assert.Equal(_support, request.DecidedById);
        Assert.Equal("در حال بررسی", request.ReviewNote);

        await _factory.WithDbAsync(async db =>
        {
            var entry = await db.ReturnTimelineEvents.AsNoTracking()
                .Where(e => e.ReturnRequestId == _returnId && e.Status == ReturnStatus.Reviewing)
                .SingleAsync();

            Assert.Equal(ReturnStatus.Submitted, entry.FromStatus);
            Assert.Equal(_support, entry.ActorId);
        });
    }

    /// <summary>The tracker must not record a parcel being un-received.</summary>
    [Fact]
    public async Task A_request_cannot_be_moved_backwards()
    {
        await ReachAsync("reviewing", "approved");

        var response = await DecideAsync("reviewing");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ReturnStatus.Approved, (await ReadReturnAsync()).Status);
    }

    /// <summary>
    /// Receiving the parcel puts the returned units back and says why they moved.
    /// </summary>
    /// <remarks>
    /// Only the units on the request, not the whole order line — half a delivery
    /// coming back is the ordinary case, and restocking wholesale would invent
    /// stock for the half the customer kept.
    /// </remarks>
    [Fact]
    public async Task Receiving_with_restock_puts_only_the_returned_units_back()
    {
        await ReachAsync("reviewing", "approved");

        (await DecideAsync("received", restock: true)).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var product = await db.Products.AsNoTracking().FirstAsync(p => p.Id == _productId);
            Assert.Equal(StartingStock + Returning, product.Stock);

            var movement = await db.StockMovements.AsNoTracking()
                .SingleAsync(m => m.ProductId == _productId);
            Assert.Equal(Returning, movement.Quantity);
            Assert.Equal(_support, movement.ActorId);
        });

        Assert.True((await ReadReturnAsync()).Restocked);
    }

    /// <summary>A parcel that came back damaged is received without going on the shelf.</summary>
    [Fact]
    public async Task Receiving_without_restock_leaves_the_count_alone()
    {
        await ReachAsync("reviewing", "approved");

        (await DecideAsync("received", restock: false)).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var product = await db.Products.AsNoTracking().FirstAsync(p => p.Id == _productId);
            Assert.Equal(StartingStock, product.Stock);
            Assert.Empty(await db.StockMovements.AsNoTracking().ToListAsync());
        });

        Assert.False((await ReadReturnAsync()).Restocked);
    }

    /// <summary>
    /// The refund is the order's own arithmetic, not the request's claim.
    /// </summary>
    /// <remarks>
    /// Two units at the line price the order froze — nothing in the request body
    /// names an amount, which is the point: a decision endpoint that took one
    /// would be a way to credit a wallet with a number of the caller's choosing.
    /// </remarks>
    [Fact]
    public async Task Refunding_credits_the_wallet_with_what_the_order_says()
    {
        await ReachAsync("reviewing", "approved", "received");

        var response = await DecideAsync("refunded");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(UnitPrice * Returning, body.GetProperty("refundedToWallet").GetInt64());
        Assert.Equal(0, body.GetProperty("manualRefund").GetInt64());

        await _factory.WithDbAsync(async db =>
        {
            var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == _customerId);
            Assert.Equal(UnitPrice * Returning, customer.WalletBalance.Amount);

            var ledger = await db.WalletTransactions.AsNoTracking()
                .SingleAsync(t => t.CustomerId == _customerId);
            Assert.Equal(UnitPrice * Returning, ledger.Amount);
        });

        var request = await ReadReturnAsync();
        Assert.Equal(ReturnStatus.Refunded, request.Status);
        Assert.Equal(UnitPrice * Returning, request.RefundAmount.Amount);
        Assert.NotNull(request.RefundedAtUtc);
    }

    /// <summary>A double-clicked approve must not pay a second refund.</summary>
    [Fact]
    public async Task A_closed_request_cannot_be_decided_again()
    {
        await ReachAsync("reviewing", "approved", "received", "refunded");

        var again = await DecideAsync("refunded");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == _customerId);
            Assert.Equal(UnitPrice * Returning, customer.WalletBalance.Amount);
            Assert.Single(await db.WalletTransactions.AsNoTracking().ToListAsync());
        });
    }

    /// <summary>
    /// Money that never arrived cannot go back.
    /// </summary>
    /// <remarks>
    /// A delivered cash-on-delivery order nobody reconciled is finished on the
    /// fulfilment path and outstanding on the payment one. Paying out against it
    /// would take cash out of the till for a sale that never went in.
    /// </remarks>
    [Fact]
    public async Task A_return_against_an_unpaid_order_cannot_be_refunded()
    {
        (await DecideAsync("reviewing", id: _unpaidReturnId)).EnsureSuccessStatusCode();
        (await DecideAsync("approved", id: _unpaidReturnId)).EnsureSuccessStatusCode();
        (await DecideAsync("received", id: _unpaidReturnId, restock: false)).EnsureSuccessStatusCode();

        var response = await DecideAsync("refunded", id: _unpaidReturnId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("order-not-paid", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await _factory.WithDbAsync(async db =>
        {
            var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == _customerId);
            Assert.Equal(Money.Zero, customer.WalletBalance);
        });
    }

    /// <summary>Rejecting is reachable from anywhere still open, and closes the request.</summary>
    [Fact]
    public async Task A_request_can_be_rejected_and_nothing_is_paid()
    {
        (await DecideAsync("rejected", note: "خارج از مهلت مرجوعی")).EnsureSuccessStatusCode();

        var request = await ReadReturnAsync();
        Assert.Equal(ReturnStatus.Rejected, request.Status);
        Assert.Equal(Money.Zero, request.RefundAmount);

        await _factory.WithDbAsync(async db =>
            Assert.Empty(await db.WalletTransactions.AsNoTracking().ToListAsync()));
    }

    /// <summary>Every decision tells the customer, the way an order's own steps do.</summary>
    [Fact]
    public async Task Deciding_tells_the_customer()
    {
        (await DecideAsync("approved")).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var code = (await db.ReturnRequests.AsNoTracking().FirstAsync(r => r.Id == _returnId)).Code;
            Assert.True(await db.CustomerNotifications.AsNoTracking()
                .AnyAsync(n => n.CustomerId == _customerId && n.Body.Contains(code)));
        });
    }

    /// <summary>
    /// A card refund pays nothing into the wallet and reports what is owed.
    /// </summary>
    /// <remarks>
    /// No adapter behind <c>IPaymentGateway</c> can reverse a charge, so the
    /// figure is for a person to settle at the bank. Reporting it is what stops
    /// the operator assuming the money already moved.
    /// </remarks>
    [Fact]
    public async Task A_card_refund_is_reported_rather_than_paid()
    {
        Guid cardReturnId = default;

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.Include(o => o.Lines).FirstAsync(o => o.Id == _orderId);
            var product = await db.Products.FirstAsync(p => p.Id == _productId);

            var request = Filed(order, product, _customerId, "card");
            db.ReturnRequests.Add(request);
            await db.SaveChangesAsync();
            cardReturnId = request.Id;
        });

        var response = await DecideAsync("refunded", id: cardReturnId);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("refundedToWallet").GetInt64());
        Assert.Equal(UnitPrice * Returning, body.GetProperty("manualRefund").GetInt64());

        await _factory.WithDbAsync(async db =>
        {
            var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == _customerId);
            Assert.Equal(Money.Zero, customer.WalletBalance);
        });
    }

    /// <summary>
    /// The queue shows what a request would pay before anyone approves it.
    /// </summary>
    /// <remarks>
    /// Computed by <c>ReturnRefund</c> rather than a second implementation of
    /// it, so the figure quoted and the figure paid cannot disagree.
    /// </remarks>
    [Fact]
    public async Task The_queue_quotes_the_refund_the_decision_will_pay()
    {
        var client = _factory.CreateAdminClient(_support);

        var response = await client.GetAsync($"/api/admin/returns/{_returnId}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(UnitPrice * Returning, body.GetProperty("refundEstimate").GetInt64());
        Assert.True(body.GetProperty("payable").GetBoolean());
        Assert.Equal("submitted", body.GetProperty("status").GetString());
        Assert.Equal(Returning, body.GetProperty("items")[0].GetProperty("quantity").GetInt32());
        Assert.Equal(UnitPrice, body.GetProperty("items")[0].GetProperty("unitPrice").GetInt64());
    }

    /// <summary>An unpaid order's return is listed with the refusal visible before the operator clicks.</summary>
    [Fact]
    public async Task The_queue_lists_open_requests_and_flags_the_unpayable_one()
    {
        var client = _factory.CreateAdminClient(_support);

        var response = await client.GetAsync("/api/admin/returns");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("total").GetInt32());

        var unpayable = body.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == _unpaidReturnId.ToString());

        Assert.False(unpayable.GetProperty("payable").GetBoolean());
        Assert.Equal(0, unpayable.GetProperty("refundEstimate").GetInt64());
    }

    /// <summary>
    /// A partial return leaves the order paid — most of it was kept.
    /// </summary>
    /// <remarks>
    /// <see cref="OrderPaymentStatus.Refunded"/> is a fact about the whole order,
    /// so setting it here would put a repayment in the books that never happened.
    /// </remarks>
    [Fact]
    public async Task A_partial_return_does_not_mark_the_order_refunded()
    {
        await ReachAsync("reviewing", "approved", "received", "refunded");

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == _orderId);
            Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        });
    }

    /// <summary>Once everything the order sold has come back, its money has too.</summary>
    [Fact]
    public async Task Returning_every_unit_marks_the_order_refunded()
    {
        Guid restId = default;

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.Include(o => o.Lines).FirstAsync(o => o.Id == _orderId);
            var requestId = Guid.NewGuid();

            // The other two of the four units, so the order's whole goods value
            // is accounted for across the two requests.
            var rest = ReturnRequest.Create(
                OrderNumber.NewReturnCode(),
                _customerId,
                order.Id,
                order.Number,
                "کالا آسیب دیده بود",
                null,
                "wallet",
                [new ReturnItem
                {
                    ReturnRequestId = requestId,
                    ProductId = _productId,
                    ProductSlug = "p-ret",
                    ProductTitle = "محصول",
                    ProductImageUrl = "https://example.test/p.jpg",
                    Quantity = Ordered - Returning,
                }],
                DateTimeOffset.UtcNow);

            db.ReturnRequests.Add(rest);
            await db.SaveChangesAsync();
            restId = rest.Id;
        });

        await ReachAsync("reviewing", "approved", "received", "refunded");
        (await DecideAsync("refunded", id: restId)).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == _orderId);
            Assert.Equal(OrderPaymentStatus.Refunded, order.PaymentStatus);
        });
    }
}
