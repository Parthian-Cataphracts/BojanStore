using Bojan.Domain.Common;
using Bojan.Domain.Orders;

namespace Bojan.Domain.Tests;

/// <summary>
/// An order used to carry no record of whether it had been paid for.
/// </summary>
/// <remarks>
/// Stock was decremented, the coupon was consumed, and nothing anywhere could
/// answer "did the money arrive". The state below follows Phonix's order
/// lifecycle, which settles a payment by having an operator confirm a receipt
/// rather than by trusting a gateway that approves everything.
/// </remarks>
public class OrderPaymentStateTests
{
    private static OrderLineDraft Line() => new(
        ProductId: Guid.NewGuid(),
        ProductSlug: "p-1",
        ProductTitle: "محصول",
        ProductImageUrl: "https://example.com/p.jpg",
        Quantity: 1,
        UnitPrice: new Money(100_000));

    private static Order Make(string paymentMethodCode, long walletPaid = 0, long shipping = 0) =>
        Order.Create(
            number: "BJ-100001",
            customerId: Guid.NewGuid(),
            lines: [Line()],
            shippingAddressId: Guid.NewGuid(),
            shippingAddressSnapshot: "تهران",
            shippingMethodName: "پست پیشتاز",
            paymentMethodName: "روش",
            paymentMethodCode: paymentMethodCode,
            subtotal: new Money(100_000),
            discount: Money.Zero,
            shipping: new Money(shipping),
            idempotencyKey: Guid.NewGuid().ToString(),
            walletPaid: new Money(walletPaid));

    [Fact]
    public void An_order_starts_awaiting_payment()
    {
        Assert.Equal(OrderPaymentStatus.AwaitingPayment, Make("gateway").PaymentStatus);
    }

    [Fact]
    public void A_wallet_covered_order_is_paid_on_placement()
    {
        // The balance was debited in the same transaction that created it, so
        // there is nothing left to collect and no person to attribute.
        var order = Make("wallet", walletPaid: 100_000);

        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.NotNull(order.PaidAtUtc);
        Assert.Null(order.SettledById);
    }

    [Fact]
    public void A_partly_wallet_covered_order_still_awaits_the_rest()
    {
        var order = Make("gateway", walletPaid: 60_000, shipping: 45_000);

        Assert.Equal(OrderPaymentStatus.AwaitingPayment, order.PaymentStatus);
    }

    [Fact]
    public void Settling_records_who_confirmed_it_and_against_what()
    {
        var order = Make("gateway");
        var operatorId = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;

        Assert.True(order.MarkPaid(at, "TRK-99", operatorId));

        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(at, order.PaidAtUtc);
        Assert.Equal("TRK-99", order.PaymentReference);
        Assert.Equal(operatorId, order.SettledById);
    }

    [Fact]
    public void Settling_twice_changes_nothing()
    {
        var order = Make("gateway");
        var first = Guid.NewGuid();
        order.MarkPaid(DateTimeOffset.UtcNow, "first", first);

        // A double-clicked approve button, or two operators working the queue.
        Assert.False(order.MarkPaid(DateTimeOffset.UtcNow, "second", Guid.NewGuid()));
        Assert.Equal("first", order.PaymentReference);
        Assert.Equal(first, order.SettledById);
    }

    [Fact]
    public void A_refused_attempt_does_not_settle_the_order()
    {
        var order = Make("gateway");

        Assert.True(order.MarkPaymentFailed("declined-1"));
        Assert.Equal(OrderPaymentStatus.Failed, order.PaymentStatus);
        Assert.Null(order.PaidAtUtc);
    }

    [Fact]
    public void Only_a_paid_order_can_be_refunded()
    {
        var unpaid = Make("gateway");
        Assert.False(unpaid.MarkRefunded());
        Assert.Equal(OrderPaymentStatus.AwaitingPayment, unpaid.PaymentStatus);

        var paid = Make("wallet", walletPaid: 100_000);
        Assert.True(paid.MarkRefunded());
        Assert.Equal(OrderPaymentStatus.Refunded, paid.PaymentStatus);
    }

    // --- the fulfilment gate ------------------------------------------------

    [Fact]
    public void An_unpaid_order_can_be_prepared_and_packed()
    {
        // Picking and packing against a transfer that has not cleared is
        // ordinary; card-to-card takes hours.
        var order = Make("gateway");

        order.TransitionTo(OrderStatus.Processing);
        order.TransitionTo(OrderStatus.Packed);

        Assert.Equal(OrderStatus.Packed, order.Status);
    }

    [Fact]
    public void An_unpaid_order_cannot_ship()
    {
        var order = Make("gateway");
        order.TransitionTo(OrderStatus.Processing);
        order.TransitionTo(OrderStatus.Packed);

        var refusal = Assert.Throws<OrderNotPaidException>(() => order.TransitionTo(OrderStatus.Shipped));

        Assert.Equal("BJ-100001", refusal.OrderNumber);
        Assert.Equal(OrderStatus.Shipped, refusal.AttemptedStatus);
        Assert.Equal(OrderStatus.Packed, order.Status);
    }

    [Fact]
    public void Settling_the_payment_releases_the_order_to_ship()
    {
        var order = Make("gateway");
        order.TransitionTo(OrderStatus.Processing);
        order.TransitionTo(OrderStatus.Packed);

        order.MarkPaid(DateTimeOffset.UtcNow, "TRK-1", Guid.NewGuid());
        order.TransitionTo(OrderStatus.Shipped);

        Assert.Equal(OrderStatus.Shipped, order.Status);
    }

    [Fact]
    public void Cash_on_delivery_ships_while_still_outstanding()
    {
        // It is unpaid by definition until the courier collects, so refusing to
        // ship it would refuse the method entirely.
        var order = Make("cod");

        order.TransitionTo(OrderStatus.Processing);
        order.TransitionTo(OrderStatus.Packed);
        order.TransitionTo(OrderStatus.Shipped);
        order.TransitionTo(OrderStatus.Delivered);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(OrderPaymentStatus.AwaitingPayment, order.PaymentStatus);
    }

    [Fact]
    public void An_unpaid_order_can_still_be_cancelled()
    {
        // Cancelling is how an unpaid order is disposed of; the gate must not
        // trap it.
        var order = Make("gateway");
        order.TransitionTo(OrderStatus.Processing);

        order.TransitionTo(OrderStatus.Cancelled);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    // --- the timeline -------------------------------------------------------

    [Fact]
    public void A_transition_records_where_it_came_from_and_who_moved_it()
    {
        var order = Make("cod");
        var operatorId = Guid.NewGuid();

        order.TransitionTo(OrderStatus.Processing, actorId: operatorId, reason: "پرداخت تأیید شد");

        var entry = order.Timeline.Last();
        Assert.Equal(OrderStatus.Pending, entry.FromStatus);
        Assert.Equal(OrderStatus.Processing, entry.Status);
        Assert.Equal(operatorId, entry.ActorId);
        Assert.Equal("پرداخت تأیید شد", entry.Reason);
    }

    [Fact]
    public void The_creation_entry_has_no_previous_status()
    {
        Assert.Null(Make("gateway").Timeline.Single().FromStatus);
    }
}
