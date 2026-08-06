using Bojan.Domain.Common;
using Bojan.Domain.Orders;

namespace Bojan.Domain.Tests;

/// <summary>
/// What sending goods back is worth, and the order a return may travel in.
/// </summary>
/// <remarks>
/// Both halves are money rules in disguise. The refund decides what leaves the
/// till, and the transition rules decide how many times it can leave: a request
/// that can be moved backwards, or refunded twice, is a request that pays twice.
/// </remarks>
public class ReturnRefundTests
{
    private const long UnitPrice = 200_000;
    private const int Ordered = 4;

    /// <summary>An order of four units at 200,000, with whatever discount the case needs.</summary>
    private static Order OrderOf(long discount = 0, long shipping = 0, bool paid = true)
    {
        var productId = Guid.NewGuid();

        var order = Order.Create(
            OrderNumber.NewOrderNumber(),
            Guid.NewGuid(),
            [new OrderLineDraft(productId, "p-01", "محصول", "https://example.test/p.jpg", Ordered, new Money(UnitPrice))],
            Guid.NewGuid(),
            "تهران",
            "پست پیشتاز",
            "پرداخت اینترنتی",
            "gateway",
            subtotal: new Money(UnitPrice * Ordered),
            discount: new Money(discount),
            shipping: new Money(shipping),
            idempotencyKey: Guid.NewGuid().ToString());

        if (paid)
        {
            order.MarkPaid(DateTimeOffset.UtcNow, "TRK-1", Guid.NewGuid());
        }

        return order;
    }

    private static ReturnItem[] Returning(Order order, int quantity) =>
        [new ReturnItem
        {
            ReturnRequestId = Guid.NewGuid(),
            ProductId = order.Lines.First().ProductId,
            ProductSlug = "p-01",
            ProductTitle = "محصول",
            ProductImageUrl = "https://example.test/p.jpg",
            Quantity = quantity,
        }];

    [Fact]
    public void A_return_is_worth_the_line_price_times_what_comes_back()
    {
        var order = OrderOf();

        var outcome = ReturnRefund.For(order, Returning(order, 2));

        Assert.Equal(UnitPrice * 2, outcome.Refund.Amount);
        Assert.True(outcome.Payable);
    }

    /// <summary>
    /// An order-level discount is shared across the goods in proportion to what
    /// they cost.
    /// </summary>
    /// <remarks>
    /// Refunding the undiscounted line value would let a customer who bought
    /// 800,000 of goods for 600,000 send half back and be repaid 400,000 — two
    /// thirds of what they actually paid, for half of what they actually got.
    /// </remarks>
    [Fact]
    public void An_order_discount_is_shared_across_what_comes_back()
    {
        var order = OrderOf(discount: 200_000);

        var outcome = ReturnRefund.For(order, Returning(order, 2));

        // Half the goods came back, so half the discount is withheld:
        // 400,000 of line value less 100,000 of discount share.
        Assert.Equal(300_000, outcome.Refund.Amount);
    }

    /// <summary>Returning everything gives back exactly what was paid for goods.</summary>
    [Fact]
    public void Returning_the_whole_order_refunds_the_whole_discounted_total()
    {
        var order = OrderOf(discount: 200_000);

        var outcome = ReturnRefund.For(order, Returning(order, Ordered));

        Assert.Equal(order.Subtotal.ClampedMinus(order.Discount).Amount, outcome.Refund.Amount);
    }

    /// <summary>The parcel was carried and delivered; that service is not undone by the goods coming back.</summary>
    [Fact]
    public void Shipping_is_never_refunded()
    {
        var order = OrderOf(shipping: 45_000);

        var outcome = ReturnRefund.For(order, Returning(order, Ordered));

        Assert.Equal(UnitPrice * Ordered, outcome.Refund.Amount);
        Assert.NotEqual(order.Total.Amount, outcome.Refund.Amount);
    }

    /// <summary>
    /// Money that never arrived cannot go back.
    /// </summary>
    /// <remarks>
    /// A delivered cash-on-delivery order nobody has reconciled is the realistic
    /// case: finished on the fulfilment path, outstanding on the payment one.
    /// The goods value is still reported, so the panel can say why it refused
    /// rather than showing a bare zero.
    /// </remarks>
    [Fact]
    public void An_order_that_was_never_paid_refunds_nothing()
    {
        var order = OrderOf(paid: false);

        var outcome = ReturnRefund.For(order, Returning(order, 2));

        Assert.False(outcome.Payable);
        Assert.Equal(0, outcome.Refund.Amount);
        Assert.Equal(UnitPrice * 2, outcome.GoodsValue.Amount);
    }

    /// <summary>A product the order never contained contributes nothing rather than throwing.</summary>
    [Fact]
    public void An_item_with_no_matching_line_is_worth_nothing()
    {
        var order = OrderOf();

        var outcome = ReturnRefund.For(order, [new ReturnItem
        {
            ReturnRequestId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            ProductSlug = "gone",
            ProductTitle = "محصول",
            ProductImageUrl = "https://example.test/p.jpg",
            Quantity = 1,
        }]);

        Assert.Equal(0, outcome.Refund.Amount);
    }

    // --- the transition rules ------------------------------------------------

    private static ReturnRequest NewRequest() =>
        ReturnRequest.Create(
            OrderNumber.NewReturnCode(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BZ-123456-ABCD",
            "کالا آسیب دیده بود",
            null,
            "wallet",
            [new ReturnItem
            {
                ReturnRequestId = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                ProductSlug = "p-01",
                ProductTitle = "محصول",
                ProductImageUrl = "https://example.test/p.jpg",
                Quantity = 1,
            }],
            DateTimeOffset.UtcNow);

    [Fact]
    public void A_request_starts_submitted_with_one_timeline_entry()
    {
        var request = NewRequest();

        Assert.Equal(ReturnStatus.Submitted, request.Status);
        Assert.Single(request.Timeline);
        Assert.Equal(Money.Zero, request.RefundAmount);
    }

    [Fact]
    public void A_transition_records_where_it_came_from_and_who_moved_it()
    {
        var request = NewRequest();
        var actor = Guid.NewGuid();

        var entry = request.TransitionTo(ReturnStatus.Reviewing, DateTimeOffset.UtcNow, actor, "در حال بررسی");

        Assert.Equal(ReturnStatus.Submitted, entry.FromStatus);
        Assert.Equal(actor, entry.ActorId);
        Assert.Equal("در حال بررسی", entry.Reason);
        Assert.Equal(actor, request.DecidedById);
    }

    /// <summary>
    /// The tracker must not record a parcel being un-received.
    /// </summary>
    /// <remarks>
    /// This is the rule the method claimed and did not have: any destination was
    /// accepted from any open state, so an approved return could be sent back to
    /// submitted and both entries were kept.
    /// </remarks>
    [Fact]
    public void A_request_cannot_move_backwards()
    {
        var request = NewRequest();
        request.TransitionTo(ReturnStatus.Approved, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.TransitionTo(ReturnStatus.Reviewing, DateTimeOffset.UtcNow));
    }

    /// <summary>Re-sending the state it is already in appended a second entry and notified the customer twice.</summary>
    [Fact]
    public void A_request_cannot_be_moved_to_where_it_already_is()
    {
        var request = NewRequest();
        request.TransitionTo(ReturnStatus.Reviewing, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.TransitionTo(ReturnStatus.Reviewing, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Rejecting is reachable from anywhere still open — including after the
    /// parcel arrived, which is the first time anyone has seen what came back.
    /// </summary>
    [Theory]
    [InlineData(ReturnStatus.Submitted)]
    [InlineData(ReturnStatus.Reviewing)]
    [InlineData(ReturnStatus.Approved)]
    [InlineData(ReturnStatus.Received)]
    public void A_request_can_be_rejected_from_any_open_state(ReturnStatus from)
    {
        var request = NewRequest();
        if (from is not ReturnStatus.Submitted)
        {
            request.TransitionTo(from, DateTimeOffset.UtcNow);
        }

        request.TransitionTo(ReturnStatus.Rejected, DateTimeOffset.UtcNow);

        Assert.Equal(ReturnStatus.Rejected, request.Status);
        Assert.True(request.IsClosed);
    }

    /// <summary>
    /// Reaching the closing state without naming an amount would close a request
    /// that paid nothing back, so the plain transition refuses it outright.
    /// </summary>
    [Fact]
    public void Refunded_cannot_be_reached_without_naming_an_amount()
    {
        var request = NewRequest();
        request.TransitionTo(ReturnStatus.Received, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.TransitionTo(ReturnStatus.Refunded, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Refunding_records_the_amount_and_closes_the_request()
    {
        var request = NewRequest();
        request.TransitionTo(ReturnStatus.Received, DateTimeOffset.UtcNow);

        var at = DateTimeOffset.UtcNow;
        request.Refund(new Money(300_000), at, Guid.NewGuid(), "تسویه شد");

        Assert.Equal(ReturnStatus.Refunded, request.Status);
        Assert.Equal(300_000, request.RefundAmount.Amount);
        Assert.Equal(at, request.RefundedAtUtc);
        Assert.True(request.IsClosed);
    }

    /// <summary>A double-clicked approve must not pay a second refund.</summary>
    [Fact]
    public void A_closed_request_cannot_be_refunded_again()
    {
        var request = NewRequest();
        request.Refund(new Money(300_000), DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.Refund(new Money(300_000), DateTimeOffset.UtcNow));

        Assert.Equal(300_000, request.RefundAmount.Amount);
    }

    /// <summary>Restocking is a separate fact: a damaged return is received without being put back.</summary>
    [Fact]
    public void Receiving_does_not_restock_on_its_own()
    {
        var request = NewRequest();
        request.TransitionTo(ReturnStatus.Received, DateTimeOffset.UtcNow);

        Assert.False(request.Restocked);

        request.MarkRestocked();

        Assert.True(request.Restocked);
    }
}
