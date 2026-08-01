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

    /// <summary>Estimated delivery window shown on the checkout screen, e.g. "۲ تا ۳ روز کاری".</summary>
    public string? Estimate { get; set; }

    /// <summary>Material Symbols name drawn beside the option.</summary>
    public string Icon { get; set; } = "local_shipping";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
