namespace Bojan.Domain.Orders;

/// <summary>
/// Whether the money for an order has actually been collected.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately separate from <see cref="OrderStatus"/>. That one describes
/// where the parcel is — pending, packed, shipped, delivered — and an order can
/// sit at any point on it while the money is still outstanding. Folding the two
/// together is what left this system unable to answer "has this been paid for":
/// an order was created, stock was decremented, the coupon was consumed, and
/// nothing anywhere recorded whether a single Toman had arrived.
/// </para>
/// <para>
/// The vocabulary follows Phonix's order lifecycle, which settles a payment by
/// having an operator approve a receipt rather than by trusting a gateway
/// callback. That is the right shape here too: the only
/// <see cref="Bojan.Application.Common.IPaymentGateway"/> implementation is a
/// sandbox that approves everything, so "the gateway said yes" is not evidence
/// of anything. A person confirming a transfer against a bank statement is.
/// </para>
/// </remarks>
public enum OrderPaymentStatus
{
    /// <summary>
    /// Nothing has been collected yet.
    /// </summary>
    /// <remarks>
    /// Where every order starts unless the wallet covered it outright. For cash
    /// on delivery this is the normal resting state until the courier hands the
    /// money over — which is why that method is exempt from the rule that an
    /// unpaid order cannot ship.
    /// </remarks>
    AwaitingPayment,

    /// <summary>The money is in, and who confirmed it is recorded on the order.</summary>
    Paid,

    /// <summary>Collected and given back — a cancellation or a settled return.</summary>
    Refunded,

    /// <summary>An attempt was made and refused. The order stays, the payment does not.</summary>
    Failed,
}
