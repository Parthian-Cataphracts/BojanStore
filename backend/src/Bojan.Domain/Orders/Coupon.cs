using Bojan.Domain.Common;

namespace Bojan.Domain.Orders;

/// <summary>
/// A discount code.
/// </summary>
/// <remarks>
/// Mirrors the admin's <c>coupons</c> writable resource fields exactly
/// (<c>apps/admin/src/lib/api/resources.ts</c>): <c>code, percent, amount,
/// minimumSpend, expiresAt, status</c>. A coupon is either a percentage or a
/// fixed <see cref="AmountOff"/>, never both — <see cref="Validate"/> is the
/// one place that decides which, so <c>/cart/coupon</c> (Phase 4) and order
/// placement (which re-validates every coupon rather than trusting the
/// client) share the same rule.
/// </remarks>
public sealed class Coupon : Entity
{
    public required string Code { get; set; }

    public int? PercentOff { get; set; }

    public Money? AmountOff { get; set; }

    public Money? MinimumSpend { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>How many times any customer may redeem this code — null means unlimited.</summary>
    public int? MaxRedemptions { get; set; }

    public int RedemptionCount { get; private set; }

    /// <summary>
    /// Computes the discount for a given subtotal, or throws if the coupon
    /// does not apply. Never returns more than the subtotal — the caller does
    /// not need to clamp.
    /// </summary>
    public Money Validate(Money subtotal, DateTimeOffset nowUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("This coupon is no longer active.");
        }

        if (ExpiresAtUtc is { } expires && expires <= nowUtc)
        {
            throw new InvalidOperationException("This coupon has expired.");
        }

        if (MaxRedemptions is { } max && RedemptionCount >= max)
        {
            throw new InvalidOperationException("This coupon has reached its redemption limit.");
        }

        if (MinimumSpend is { } minimum && subtotal < minimum)
        {
            throw new InvalidOperationException("The order does not meet this coupon's minimum spend.");
        }

        var discount = PercentOff is { } percent
            ? new Money(subtotal.Amount * percent / 100)
            : AmountOff ?? Money.Zero;

        // A fixed-amount coupon must never discount more than the goods it applies to.
        return discount > subtotal ? subtotal : discount;
    }

    public void RecordRedemption() => RedemptionCount++;
}
