namespace Bojan.Application.Contracts;

/// <summary>
/// One shipping tier as the panel edits it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Code"/> is the wire id the checkout submits, and it is the one
/// field the panel cannot change: the storefront's checkout screens name these
/// tiers as presentation constants, so renaming one here would leave a shopper
/// submitting an id the shop no longer has. Tiers are edited, not created and
/// deleted, for the same reason.
/// </para>
/// <para>
/// The price is in Toman, like every other figure in this system.
/// </para>
/// </remarks>
public sealed record AdminShippingMethodDto(
    string Code,
    string Title,
    long Price,
    string Estimate,
    bool IsActive);

/// <summary>What the shipping settings screen submits — the whole list, replaced.</summary>
public sealed record SaveShippingMethodsRequest(IReadOnlyList<AdminShippingMethodDto> Methods);
