using Bojan.Domain.Common;

namespace Bojan.Domain.Catalogue;

/// <summary>
/// "Tell me when this is back" — the public write behind
/// <c>POST /stock-alerts</c>.
/// </summary>
/// <remarks>
/// Deliberately not tied to a customer: the frontend's allow-list marks this
/// action <c>private: false</c>, so a visitor with no account may leave a phone
/// number or an email address (see
/// <c>apps/storefront/src/app/api/account/[action]/route.ts</c>).
/// </remarks>
public sealed class StockAlert : Entity
{
    public required Guid ProductId { get; init; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>Set when the customer has actually been told, so a restock never notifies twice.</summary>
    public DateTimeOffset? NotifiedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
