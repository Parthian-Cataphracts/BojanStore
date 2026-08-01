using Bojan.Domain.Common;

namespace Bojan.Domain.Orders;

/// <summary>
/// A selectable payment method — the id the checkout submits as
/// <c>paymentMethodId</c>, validated at order placement per <c>BACKEND.md</c>
/// Phase 4, rule 4.
/// </summary>
/// <remarks>
/// <see cref="Code"/> is the wire id (<c>gateway</c>, <c>wallet</c>,
/// <c>cod</c>) for the reason given on <see cref="ShippingMethod.Code"/>.
/// </remarks>
public sealed class PaymentMethod : Entity
{
    /// <summary>Stable wire id — the value the checkout submits, not the GUID.</summary>
    public required string Code { get; init; }

    public required string Title { get; set; }

    /// <summary>True for a gateway redirect (<c>Order.PaymentUrl</c> gets set); false for cash on delivery.</summary>
    public bool RequiresGateway { get; set; }

    /// <summary>True when the order is paid from the customer's wallet balance rather than a card.</summary>
    public bool UsesWallet { get; set; }

    /// <summary>Material Symbols name drawn beside the option.</summary>
    public string Icon { get; set; } = "credit_card";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
