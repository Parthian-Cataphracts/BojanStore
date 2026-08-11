using Bojan.Domain.Common;

namespace Bojan.Domain.Orders;

/// <summary>
/// A selectable shipping tier. Backs <c>GET /shipping-methods</c>
/// (<c>BACKEND.md</c> Phase 4) and the id the checkout submits as
/// <c>shippingMethodId</c>.
/// </summary>
/// <remarks>
/// <see cref="Code"/> exists because the checkout does not learn these ids
/// from the API. <c>apps/storefront/src/app/api/orders/route.ts</c> validates
/// <c>shippingMethodId</c> against the fixture in
/// <c>lib/mock/checkout.ts</c> — <c>standard</c>, <c>express</c>,
/// <c>courier</c> — in both mock and real mode, because the tiers are drawn on
/// the checkout screens as presentation constants (see the README's note on
/// fixtures that are "closer to copy than to data"). So the wire id has to be
/// that string and the GUID stays internal. The same applies to
/// <see cref="PaymentMethod.Code"/>.
/// </remarks>
public sealed class ShippingMethod : Entity
{
    /// <summary>Stable wire id — the value the checkout submits, not the GUID.</summary>
    public required string Code { get; init; }

    public required string Title { get; set; }

    public required Money Price { get; set; }

    /// <summary>
    /// What the order has to come to for this method to cost nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three states, and an operator picks between them on the shipping screen:
    /// <c>null</c> is always charged, <c>0</c> is always free, and any other
    /// figure is free at or above it. A courier that is never free and a post
    /// tier that is free over a million are both ordinary, and one shop wants
    /// both at once.
    /// </para>
    /// <para>
    /// Per method rather than one figure for the shop, because the shop-wide
    /// version could not express that — and because two places holding the same
    /// rule is how they come to disagree. This is the only source of it: the
    /// checkout reads the chosen method's, and what the storefront advertises is
    /// derived from the active methods rather than stored beside them.
    /// </para>
    /// </remarks>
    public long? FreeAboveAmount { get; set; }

    /// <summary>Estimated delivery window shown on the checkout screen, e.g. "۲ تا ۳ روز کاری".</summary>
    public string? Estimate { get; set; }

    /// <summary>Material Symbols name drawn beside the option.</summary>
    public string Icon { get; set; } = "local_shipping";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
