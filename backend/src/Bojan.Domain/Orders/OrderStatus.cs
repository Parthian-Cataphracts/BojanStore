namespace Bojan.Domain.Orders;

/// <summary>
/// The full order lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// This is the superset of two frontend vocabularies that do not currently
/// agree, and the API has to speak both:
/// </para>
/// <list type="bullet">
/// <item>The storefront's <c>OrderStatus</c>
/// (<c>apps/storefront/src/lib/api/types.ts</c>) has six values and no
/// <see cref="Packed"/>.</item>
/// <item>The admin panel's <c>AdminOrderStatus</c>
/// (<c>apps/admin/src/lib/types.ts</c>) has seven, including
/// <see cref="Packed"/> — used for the "picked, boxed, not yet handed to a
/// carrier" step the operator screens track.</item>
/// </list>
/// <para>
/// Until that is reconciled on the frontend, whichever endpoint serves the
/// storefront must map <see cref="Packed"/> onto <see cref="Processing"/>
/// rather than emit a seventh value the customer-facing type does not expect
/// (would fail the DTO shape check in <c>BACKEND.md</c>'s definition of
/// done). The admin endpoint emits it as-is.
/// </para>
/// </remarks>
public enum OrderStatus
{
    Pending,
    Processing,
    Packed,
    Shipped,
    Delivered,
    Cancelled,
    Returned,
}
