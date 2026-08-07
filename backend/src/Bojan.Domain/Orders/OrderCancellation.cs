using Bojan.Domain.Common;

namespace Bojan.Domain.Orders;

/// <summary>
/// What cancelling an order costs, gives back, and is allowed to do at all.
/// </summary>
/// <remarks>
/// <para>
/// The fulfilment path is payment, initial confirmation, picking from the
/// warehouse, dispatch, delivery — <see cref="OrderStatus.Pending"/>,
/// <see cref="OrderStatus.Processing"/>, <see cref="OrderStatus.Packed"/>,
/// <see cref="OrderStatus.Shipped"/>, <see cref="OrderStatus.Delivered"/>.
/// Where a cancellation lands on that path decides all three answers, so they
/// are written here as one thing rather than as conditions spread across the
/// service that happens to call them.
/// </para>
/// <para>
/// Kept in the domain, free of any storage concern, for the same reason
/// Phonix's <c>OrderRules</c> is: the figure a customer is quoted before
/// confirming and the figure that is actually refunded have to be computed by
/// the same code, and they are read from two different places.
/// </para>
/// </remarks>
public static class OrderCancellation
{
    /// <summary>
    /// Whether an order in this state can still be cancelled.
    /// </summary>
    /// <remarks>
    /// A delivered order is not cancelled, it is returned — that is what
    /// <see cref="ReturnRequest"/> and <see cref="OrderStatus.Returned"/> are
    /// for, and they carry an inspection step cancellation has no place for.
    /// Already-cancelled and already-returned are terminal.
    /// </remarks>
    public static bool CanCancel(OrderStatus status) => status
        is OrderStatus.Pending
        or OrderStatus.Processing
        or OrderStatus.Packed
        or OrderStatus.Shipped;

    /// <summary>
    /// Whether a customer cancelling at this point is charged the penalty.
    /// </summary>
    /// <remarks>
    /// From the warehouse onwards. Up to and including the initial
    /// confirmation nothing has been spent on the order but a status change, so
    /// charging for it would be charging for nothing. Once it has been picked,
    /// packed and possibly handed to a carrier, that work is real and does not
    /// come back with the goods.
    /// </remarks>
    public static bool AppliesPenalty(OrderStatus status) => status
        is OrderStatus.Packed
        or OrderStatus.Shipped;

    /// <summary>
    /// Whether the goods can be put back on the shelf without a person looking
    /// at them.
    /// </summary>
    /// <remarks>
    /// Up to and including the warehouse step the stock never left the
    /// building, so it is returned automatically. Once dispatched it is with a
    /// carrier: it may come back, it may come back damaged, or it may not come
    /// back at all, and incrementing the count on the strength of a
    /// cancellation would be inventing stock the shop does not have. That one
    /// is left for an operator to record from the inventory screen once the
    /// parcel is physically back.
    /// </remarks>
    public static bool RestocksAutomatically(OrderStatus status) => status
        is OrderStatus.Pending
        or OrderStatus.Processing
        or OrderStatus.Packed;

    /// <summary>
    /// The penalty withheld from <paramref name="refundable"/>.
    /// </summary>
    /// <remarks>
    /// Rounded away from zero so the shop never keeps a fraction more than the
    /// percentage says, and clamped to the refundable amount so a
    /// misconfigured percentage over 100 cannot turn a refund into a charge.
    /// </remarks>
    public static Money Penalty(Money refundable, decimal percent)
    {
        if (percent <= 0 || refundable == Money.Zero)
        {
            return Money.Zero;
        }

        var amount = (long)Math.Round(refundable.Amount * percent / 100m, MidpointRounding.AwayFromZero);
        return amount >= refundable.Amount ? refundable : new Money(amount);
    }

    /// <summary>
    /// What the wallet gets back, and what is withheld, for a cancellation.
    /// </summary>
    /// <param name="walletPaid">
    /// What the order actually took from the balance. The gateway's share is
    /// not here: refunding that is a call to a payment provider, and there is
    /// no adapter behind <c>IPaymentGateway</c> that can make it — see
    /// <see cref="Outcome.ManualGatewayRefund"/>.
    /// </param>
    /// <param name="chargePenalty">
    /// False when the shop cancelled rather than the customer. Charging someone
    /// for a decision that was not theirs is the one case the percentage must
    /// not apply to, however far along the order is.
    /// </param>
    /// <param name="paymentStatus">
    /// Whether the money was ever collected.
    /// </param>
    public static Outcome For(
        OrderStatus status,
        Money walletPaid,
        Money payableOnline,
        decimal percent,
        bool chargePenalty,
        OrderPaymentStatus paymentStatus)
    {
        var penalty = chargePenalty && AppliesPenalty(status)
            ? Penalty(walletPaid, percent)
            : Money.Zero;

        return new Outcome(
            Refund: walletPaid.ClampedMinus(penalty),
            Penalty: penalty,
            Restocks: RestocksAutomatically(status),
            // Only what was actually taken can be given back. This used to be
            // the whole outstanding balance regardless, so cancelling a cash-on-
            // delivery order — where nothing has been collected by definition
            // until the courier arrives — told the operator to return the full
            // price of the order to a customer who had not paid a rial.
            ManualGatewayRefund: paymentStatus is OrderPaymentStatus.Paid ? payableOnline : Money.Zero);
    }

    /// <summary>The consequences of one cancellation, all decided before anything is written.</summary>
    /// <param name="Refund">Credited back to the wallet.</param>
    /// <param name="Penalty">Withheld from <paramref name="Refund"/>; zero when none applies.</param>
    /// <param name="Restocks">Whether the lines go back into stock automatically.</param>
    /// <param name="ManualGatewayRefund">
    /// What was collected online and so has to be returned by hand. Zero for a
    /// wallet-only or cash-on-delivery order.
    /// </param>
    public readonly record struct Outcome(Money Refund, Money Penalty, bool Restocks, Money ManualGatewayRefund);
}
