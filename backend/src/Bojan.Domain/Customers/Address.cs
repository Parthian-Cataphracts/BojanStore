using Bojan.Domain.Common;

namespace Bojan.Domain.Customers;

/// <summary>
/// A delivery address belonging to one customer.
/// </summary>
/// <remarks>
/// Mirrors the frontend's <c>Address</c> DTO exactly — see
/// <c>apps/storefront/src/lib/api/types.ts</c> and the accepted fields in
/// <c>apps/storefront/src/app/api/account/[action]/route.ts</c> (the
/// <c>address</c> resource).
/// </remarks>
public sealed class Address : Entity
{
    public required Guid CustomerId { get; init; }

    /// <summary>Label the shopper gave it — "خانه", "محل کار" — not a system name.</summary>
    public required string Title { get; set; }

    public required string Recipient { get; set; }

    public required string Phone { get; set; }

    public required string Province { get; set; }

    public required string City { get; set; }

    public required string PostalCode { get; set; }

    /// <summary>Street-level address line.</summary>
    public required string Line { get; set; }

    public bool IsDefault { get; set; }

    /// <summary>
    /// When it was saved — the tiebreak for which address inherits the default
    /// flag when the one holding it is deleted or unticked.
    /// </summary>
    /// <remarks>
    /// "The one you have had longest" is the least surprising answer when the
    /// customer did not choose, and it needs an ordering the row itself carries:
    /// a GUID primary key has none.
    /// </remarks>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
