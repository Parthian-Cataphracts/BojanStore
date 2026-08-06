using Bojan.Domain.Common;
using Bojan.Domain.Orders;

namespace Bojan.Domain.Tests;

public class OrderTests
{
    private static OrderLineDraft MakeLine(int quantity = 1, long unitPrice = 100_000) => new(
        ProductId: Guid.NewGuid(),
        ProductSlug: "p-1",
        ProductTitle: "محصول",
        ProductImageUrl: "https://example.com/p.jpg",
        Quantity: quantity,
        UnitPrice: new Money(unitPrice));

    private static Order MakeOrder(Money subtotal, Money discount, Money shipping, IReadOnlyCollection<OrderLineDraft>? lines = null) =>
        Order.Create(
            number: "BJ-100001",
            customerId: Guid.NewGuid(),
            lines: lines ?? [MakeLine()],
            shippingAddressId: Guid.NewGuid(),
            shippingAddressSnapshot: "تهران، خیابان آزادی",
            shippingMethodName: "پست پیشتاز",
            paymentMethodName: "پرداخت آنلاین",
            subtotal: subtotal,
            discount: discount,
            shipping: shipping,
            idempotencyKey: Guid.NewGuid().ToString());

    [Fact]
    public void Create_rejects_an_order_with_no_lines()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MakeOrder(new Money(100_000), Money.Zero, Money.Zero, lines: []));
    }

    [Fact]
    public void Create_rejects_a_discount_larger_than_the_subtotal()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MakeOrder(new Money(100_000), new Money(200_000), Money.Zero));
    }

    [Fact]
    public void Total_is_subtotal_minus_discount_plus_shipping()
    {
        var order = MakeOrder(new Money(200_000), new Money(50_000), new Money(20_000));

        Assert.Equal(new Money(170_000), order.Total);
    }

    [Fact]
    public void New_order_starts_pending_with_one_timeline_event()
    {
        var order = MakeOrder(new Money(100_000), Money.Zero, Money.Zero);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Single(order.Timeline);
        Assert.Equal(OrderStatus.Pending, order.Timeline.Single().Status);
    }

    [Fact]
    public void TransitionTo_appends_a_timeline_event()
    {
        var order = MakeOrder(new Money(100_000), Money.Zero, Money.Zero);

        order.TransitionTo(OrderStatus.Processing);

        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Equal(2, order.Timeline.Count);
    }

    [Fact]
    public void TransitionTo_can_set_a_tracking_code()
    {
        var order = MakeOrder(new Money(100_000), Money.Zero, Money.Zero);

        order.TransitionTo(OrderStatus.Shipped, trackingCode: "TRK-123");

        Assert.Equal("TRK-123", order.TrackingCode);
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Returned)]
    public void TransitionTo_rejects_moving_out_of_a_terminal_state(OrderStatus terminal)
    {
        var order = MakeOrder(new Money(100_000), Money.Zero, Money.Zero);
        order.TransitionTo(terminal);

        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(OrderStatus.Processing));
    }

    /// <summary>
    /// The forward-only rule this method's summary has always described. Only
    /// the terminal check was implemented, so an order could be moved back and
    /// the timeline recorded both directions — a history saying the parcel went
    /// back to being prepared after it shipped, and a second notification to
    /// the customer telling them so.
    /// </summary>
    [Theory]
    [InlineData(OrderStatus.Shipped, OrderStatus.Pending)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Processing)]
    [InlineData(OrderStatus.Packed, OrderStatus.Processing)]
    [InlineData(OrderStatus.Processing, OrderStatus.Pending)]
    public void TransitionTo_rejects_moving_backwards(OrderStatus from, OrderStatus back)
    {
        var order = MakeOrder(new Money(100_000), Money.Zero, Money.Zero);
        order.TransitionTo(from);

        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(back));

        Assert.Equal(from, order.Status);
        Assert.Equal(2, order.Timeline.Count);
    }

    [Theory]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Packed)]
    [InlineData(OrderStatus.Shipped)]
    public void TransitionTo_rejects_the_status_it_is_already_at(OrderStatus status)
    {
        var order = MakeOrder(new Money(100_000), Money.Zero, Money.Zero);
        order.TransitionTo(status);

        // Re-sending the current status appended a second event and sent a
        // second notification for a change that did not happen.
        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(status));
        Assert.Equal(2, order.Timeline.Count);
    }

    [Fact]
    public void TransitionTo_still_allows_every_step_along_the_fulfilment_path()
    {
        var order = MakeOrder(new Money(100_000), Money.Zero, Money.Zero);

        order.TransitionTo(OrderStatus.Processing);
        order.TransitionTo(OrderStatus.Packed);
        order.TransitionTo(OrderStatus.Shipped);
        order.TransitionTo(OrderStatus.Delivered);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(5, order.Timeline.Count);
    }

    [Fact]
    public void TransitionTo_still_allows_cancelling_from_anywhere_along_the_path()
    {
        // Cancelled is terminal rather than further along, so the forward-only
        // rule must not stand in the cancellation service's way.
        var order = MakeOrder(new Money(100_000), Money.Zero, Money.Zero);
        order.TransitionTo(OrderStatus.Shipped);

        order.TransitionTo(OrderStatus.Cancelled);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }
}
