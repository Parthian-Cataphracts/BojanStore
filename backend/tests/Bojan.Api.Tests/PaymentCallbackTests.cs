using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Common;
using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The shopper's return from the gateway.
/// </summary>
/// <remarks>
/// <para>
/// This path did not exist. An order was handed to a gateway and nothing ever
/// asked whether the money arrived — the callback page said so in a comment
/// ("there is no verification endpoint to ask") and redirected to the success
/// screen regardless, so a real gateway would have left every online order
/// reading "در انتظار پرداخت" until an operator settled it by hand.
/// </para>
/// <para>
/// The three properties covered here are the ones the money depends on: only
/// the gateway decides, only the owner of the order can ask, and asking twice
/// settles once.
/// </para>
/// </remarks>
public sealed class PaymentCallbackTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _customer;
    private Guid _stranger;
    private Guid _orderId;
    private string _reference = string.Empty;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-callback", 300_000, stock: 5);

            var customer = await TestData.AddCustomerAsync(db, "09121114455");
            var stranger = await TestData.AddCustomerAsync(db, "09121114466");
            var address = await TestData.AddAddressAsync(db, customer.Id);

            _customer = customer.Id;
            _stranger = stranger.Id;

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

            // The reference the gateway issued, recorded against the order when
            // the payment session was started.
            _reference = $"A{Guid.NewGuid():N}";
            await db.Orders
                .Where(o => o.Id == order.Id)
                .ExecuteUpdateAsync(o => o.SetProperty(x => x.PaymentReference, _reference));
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    /// <remarks>
    /// Awaited inside rather than returned as a task: the client is disposed
    /// when this returns, and disposing one cancels whatever it still has in
    /// flight — which surfaces from the test host as "Flush was canceled on
    /// underlying PipeWriter" rather than as anything resembling the cause.
    /// </remarks>
    private async Task<HttpResponseMessage> SettleAsAsync(Guid customerId, string reference)
    {
        using var client = _factory.CreateCustomerClient(customerId);
        return await client.PostAsJsonAsync("/api/me/payments/callback", new { reference });
    }

    private async Task<OrderPaymentStatus> PaymentStatusAsync()
    {
        var status = OrderPaymentStatus.AwaitingPayment;

        await _factory.WithDbAsync(async db =>
            status = await db.Orders
                .Where(o => o.Id == _orderId)
                .Select(o => o.PaymentStatus)
                .FirstAsync());

        return status;
    }

    [Fact]
    public async Task A_returning_shopper_settles_their_own_order()
    {
        var response = await SettleAsAsync(_customer, _reference);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CallbackBody>();
        Assert.Equal("order", body!.Kind);
        Assert.True(body.Paid);

        Assert.Equal(OrderPaymentStatus.Paid, await PaymentStatusAsync());
    }

    /// <summary>
    /// A refreshed callback page calls this again, and two of them can arrive
    /// at once. Settling twice must credit nothing twice and must not fail —
    /// the shopper is looking at the outcome, not at an error.
    /// </summary>
    [Fact]
    public async Task Settling_twice_reports_the_same_outcome_and_notifies_once()
    {
        await SettleAsAsync(_customer, _reference);
        var second = await SettleAsAsync(_customer, _reference);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True((await second.Content.ReadFromJsonAsync<CallbackBody>())!.Paid);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(
                1,
                await db.CustomerNotifications.CountAsync(n => n.CustomerId == _customer)));
    }

    /// <summary>
    /// The reference is matched against the caller's own orders. Someone else's
    /// answers not-found rather than forbidden, for the reason every other
    /// customer-scoped read does: an id that exists must not be distinguishable
    /// from one that does not.
    /// </summary>
    [Fact]
    public async Task Another_customer_cannot_settle_this_order()
    {
        var response = await SettleAsAsync(_stranger, _reference);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(OrderPaymentStatus.AwaitingPayment, await PaymentStatusAsync());
    }

    [Fact]
    public async Task An_unknown_reference_settles_nothing()
    {
        var response = await SettleAsAsync(_customer, $"A{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(OrderPaymentStatus.AwaitingPayment, await PaymentStatusAsync());
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_settle_anything()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", BojanApiFactory.TrustedProxyKey);

        var response = await client.PostAsJsonAsync(
            "/api/me/payments/callback",
            new { reference = _reference });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(OrderPaymentStatus.AwaitingPayment, await PaymentStatusAsync());
    }

    private sealed record CallbackBody(string Kind, string? OrderNumber, string? Reference, bool Paid);
}
