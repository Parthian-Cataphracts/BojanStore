using Bojan.Domain.Common;

namespace Bojan.Domain.Orders;

/// <summary>
/// What sending goods back is worth.
/// </summary>
/// <remarks>
/// <para>
/// Kept in the domain and free of any storage concern, for the reason
/// <see cref="OrderCancellation"/> is: the figure quoted to an operator before
/// they approve a return and the figure actually paid back have to come from the
/// same code, and they are read from two different places.
/// </para>
/// <para>
/// Nothing here takes an amount from a caller. A return endpoint that accepted
/// one would be a way to pay a wallet whatever the request fancied — the same
/// hole <c>OrderCancellationRequest</c> and <c>WalletTopUpDecisionRequest</c>
/// are both shaped to avoid. Every figure below is derived from the order's own
/// frozen line prices.
/// </para>
/// </remarks>
public static class ReturnRefund
{
    /// <summary>
    /// What goes back for these items off this order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Priced from <see cref="OrderLine.UnitPrice"/>, which was captured when the
    /// order was placed — a product repriced since must not change what a past
    /// order refunds, the same rule that makes the line price a snapshot in the
    /// first place.
    /// </para>
    /// <para>
    /// An order-level discount is shared out across the goods in proportion to
    /// what they cost, so returning everything gives back exactly what was paid
    /// for goods and returning half gives back half. Refunding the undiscounted
    /// line value instead would let a customer keep the cheap half of an order
    /// bought with a 30% coupon and be repaid more than the expensive half cost
    /// them.
    /// </para>
    /// <para>
    /// Shipping is never part of it. The parcel was carried and delivered; that
    /// service was performed and is not undone by the goods coming back.
    /// </para>
    /// </remarks>
    public static Outcome For(Order order, IReadOnlyCollection<ReturnItem> items)
    {
        var goods = Money.Zero;

        foreach (var item in items)
        {
            var line = order.Lines.FirstOrDefault(candidate => candidate.ProductId == item.ProductId);
            if (line is not null)
            {
                goods += line.UnitPrice * item.Quantity;
            }
        }

        var discountShare = ShareOfDiscount(goods, order.Subtotal, order.Discount);
        var refundable = goods.ClampedMinus(discountShare);

        // Money that was never collected cannot be given back. A delivered
        // cash-on-delivery order the courier has not been reconciled for is the
        // realistic case: it looks finished on the fulfilment path and is still
        // outstanding on the payment one, and refunding it would pay out of the
        // till for a sale that never arrived in it.
        if (order.PaymentStatus is not OrderPaymentStatus.Paid)
        {
            return new Outcome(Money.Zero, refundable, Payable: false);
        }

        return new Outcome(refundable, refundable, Payable: true);
    }

    /// <summary>
    /// The part of the order's discount that belongs to these goods.
    /// </summary>
    /// <remarks>
    /// Rounded away from zero, so the deduction never falls short of the
    /// proportion — the same direction <see cref="OrderCancellation.Penalty"/>
    /// rounds, and for the same reason: a fraction repeatedly resolved in the
    /// customer's favour across many partial returns of one order adds up to
    /// refunding more than the order was worth.
    /// </remarks>
    private static Money ShareOfDiscount(Money goods, Money subtotal, Money discount)
    {
        if (discount == Money.Zero || subtotal == Money.Zero || goods == Money.Zero)
        {
            return Money.Zero;
        }

        var share = (long)Math.Round(
            (decimal)discount.Amount * goods.Amount / subtotal.Amount,
            MidpointRounding.AwayFromZero);

        // Clamped because the goods can never exceed the subtotal they came out
        // of — but a share larger than the value it is deducted from would turn
        // a refund into a charge, and that is not a thing to leave to arithmetic.
        return new Money(Math.Min(share, goods.Amount));
    }

    /// <summary>What one return is worth, decided before anything is written.</summary>
    /// <param name="Refund">
    /// What is actually paid back. Zero when the order was never settled — see
    /// <paramref name="Payable"/>.
    /// </param>
    /// <param name="GoodsValue">
    /// What the returned goods came to after their share of the discount,
    /// whether or not it can be paid. Reported so the panel can tell an operator
    /// that a return is worth something the order has not been paid for, rather
    /// than showing a bare zero.
    /// </param>
    /// <param name="Payable">False when the order is not in a state that can be refunded.</param>
    public readonly record struct Outcome(Money Refund, Money GoodsValue, bool Payable);
}
