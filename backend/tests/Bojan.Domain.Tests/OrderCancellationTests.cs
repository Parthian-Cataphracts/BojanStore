using Bojan.Domain.Common;
using Bojan.Domain.Orders;

namespace Bojan.Domain.Tests;

/// <summary>
/// The three questions cancelling an order asks: may it, does it cost, and do
/// the goods come back on their own.
/// </summary>
/// <remarks>
/// The fulfilment path is payment, confirmation, warehouse, dispatch, delivery.
/// Every boundary below is one of the steps on it, so a change to where the
/// penalty or the automatic restock starts fails here rather than being noticed
/// in a month of stock counts that do not add up.
/// </remarks>
public class OrderCancellationTests
{
    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Packed)]
    [InlineData(OrderStatus.Shipped)]
    public void An_order_still_in_fulfilment_can_be_cancelled(OrderStatus status) =>
        Assert.True(OrderCancellation.CanCancel(status));

    /// <summary>A delivered order is returned, not cancelled — that flow has an inspection step this one has not.</summary>
    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Returned)]
    public void A_finished_order_cannot_be_cancelled(OrderStatus status) =>
        Assert.False(OrderCancellation.CanCancel(status));

    /// <summary>Nothing has been spent on an order that is only confirmed, so there is nothing to charge for.</summary>
    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    public void No_penalty_before_the_warehouse(OrderStatus status) =>
        Assert.False(OrderCancellation.AppliesPenalty(status));

    /// <summary>Picking and packing is real work that does not come back with the goods.</summary>
    [Theory]
    [InlineData(OrderStatus.Packed)]
    [InlineData(OrderStatus.Shipped)]
    public void The_penalty_starts_at_the_warehouse(OrderStatus status) =>
        Assert.True(OrderCancellation.AppliesPenalty(status));

    /// <summary>Up to the warehouse the stock never left the building.</summary>
    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Packed)]
    public void Stock_comes_back_by_itself_until_it_is_dispatched(OrderStatus status) =>
        Assert.True(OrderCancellation.RestocksAutomatically(status));

    /// <summary>
    /// Once it is with a carrier the shop does not know it is coming back, or
    /// in what condition. Incrementing the count then would invent stock.
    /// </summary>
    [Fact]
    public void A_dispatched_order_is_restocked_by_hand() =>
        Assert.False(OrderCancellation.RestocksAutomatically(OrderStatus.Shipped));

    [Fact]
    public void The_penalty_is_a_percentage_of_what_was_paid()
    {
        var outcome = OrderCancellation.For(
            OrderStatus.Packed, new Money(200_000), Money.Zero, percent: 10m, chargePenalty: true, OrderPaymentStatus.Paid);

        Assert.Equal(20_000, outcome.Penalty.Amount);
        Assert.Equal(180_000, outcome.Refund.Amount);
    }

    /// <summary>Rounded away from zero, so the shop never keeps a fraction more than the percentage says.</summary>
    [Fact]
    public void A_fractional_penalty_rounds_away_from_zero()
    {
        var outcome = OrderCancellation.For(
            OrderStatus.Packed, new Money(1_005), Money.Zero, percent: 50m, chargePenalty: true, OrderPaymentStatus.Paid);

        Assert.Equal(503, outcome.Penalty.Amount);
        Assert.Equal(502, outcome.Refund.Amount);
    }

    /// <summary>
    /// Charging someone for a decision that was not theirs is not a penalty.
    /// The shop cancelling — no stock after confirmation, a pricing error —
    /// refunds in full however far along the order was.
    /// </summary>
    [Fact]
    public void The_shop_cancelling_costs_the_customer_nothing()
    {
        var outcome = OrderCancellation.For(
            OrderStatus.Shipped, new Money(200_000), Money.Zero, percent: 25m, chargePenalty: false, OrderPaymentStatus.Paid);

        Assert.Equal(0, outcome.Penalty.Amount);
        Assert.Equal(200_000, outcome.Refund.Amount);
    }

    /// <summary>A misconfigured percentage must not turn a refund into a charge.</summary>
    [Fact]
    public void A_penalty_over_the_amount_paid_is_capped_at_it()
    {
        var outcome = OrderCancellation.For(
            OrderStatus.Packed, new Money(50_000), Money.Zero, percent: 400m, chargePenalty: true, OrderPaymentStatus.Paid);

        Assert.Equal(50_000, outcome.Penalty.Amount);
        Assert.Equal(0, outcome.Refund.Amount);
    }

    /// <summary>
    /// What a gateway collected is reported, not refunded: returning it is a
    /// call to a payment provider and no adapter can make one.
    /// </summary>
    [Fact]
    public void The_gateways_share_is_reported_for_a_person_to_settle()
    {
        var outcome = OrderCancellation.For(
            OrderStatus.Processing, new Money(30_000), new Money(120_000), percent: 10m, chargePenalty: true, OrderPaymentStatus.Paid);

        Assert.Equal(30_000, outcome.Refund.Amount);
        Assert.Equal(120_000, outcome.ManualGatewayRefund.Amount);
    }

    /// <summary>A cash-on-delivery order that used no balance owes nothing back.</summary>
    [Fact]
    public void An_order_that_took_nothing_from_the_wallet_refunds_nothing()
    {
        var outcome = OrderCancellation.For(
            OrderStatus.Packed, Money.Zero, Money.Zero, percent: 10m, chargePenalty: true, OrderPaymentStatus.Paid);

        Assert.Equal(0, outcome.Refund.Amount);
        Assert.Equal(0, outcome.Penalty.Amount);
    }

    /// <summary>
    /// The cash-on-delivery case, which is the whole reason payment status is
    /// part of this decision.
    /// </summary>
    /// <remarks>
    /// The outstanding balance used to be reported as owed back whatever had
    /// happened, so cancelling a cash-on-delivery order — where by definition
    /// nothing is collected until the courier arrives — put the full price of
    /// the order in front of an operator as a refund to pay out.
    /// </remarks>
    [Fact]
    public void Nothing_is_owed_back_on_an_order_that_was_never_paid_for()
    {
        var outcome = OrderCancellation.For(
            OrderStatus.Packed,
            Money.Zero,
            new Money(450_000),
            percent: 10m,
            chargePenalty: true,
            OrderPaymentStatus.AwaitingPayment);

        Assert.Equal(0, outcome.ManualGatewayRefund.Amount);
        Assert.Equal(0, outcome.Refund.Amount);
    }

    /// <summary>The same order once the money is in: now it does have to go back.</summary>
    [Fact]
    public void What_was_collected_is_owed_back_once_it_was_collected()
    {
        var outcome = OrderCancellation.For(
            OrderStatus.Packed,
            Money.Zero,
            new Money(450_000),
            percent: 10m,
            chargePenalty: true,
            OrderPaymentStatus.Paid);

        Assert.Equal(450_000, outcome.ManualGatewayRefund.Amount);
    }
}
