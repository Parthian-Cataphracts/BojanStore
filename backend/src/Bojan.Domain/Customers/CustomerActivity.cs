using Bojan.Domain.Common;

namespace Bojan.Domain.Customers;

/// <summary>
/// A saved product — screen 11.
/// </summary>
/// <remarks>
/// The wishlist lives in <c>localStorage</c> on the frontend today
/// (<c>apps/storefront/src/lib/wishlist/store.tsx</c>) but the API already has
/// endpoints for it (<c>GET /me/wishlist</c>,
/// <c>POST /me/wishlist/remove</c>), so the server side is built now and the
/// reducer moves over as one file whenever the frontend is ready.
/// </remarks>
public sealed class WishlistItem : Entity
{
    public required Guid CustomerId { get; init; }

    public required Guid ProductId { get; init; }

    public DateTimeOffset AddedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>A product the customer opened — screen 57, backing <c>GET /me/recently-viewed</c>.</summary>
public sealed class RecentlyViewedItem : Entity
{
    public required Guid CustomerId { get; init; }

    public required Guid ProductId { get; init; }

    public DateTimeOffset ViewedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>One past search term — cleared wholesale by <c>POST /me/search-history/clear</c>.</summary>
public sealed class SearchHistoryEntry : Entity
{
    public required Guid CustomerId { get; init; }

    public required string Term { get; set; }

    public DateTimeOffset SearchedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>The four notification kinds the frontend's <c>NotificationKind</c> declares.</summary>
public enum NotificationKind
{
    Order,
    Offer,
    Account,
    Stock,
}

/// <summary>An in-app notification — screen 53.</summary>
public sealed class CustomerNotification : Entity
{
    public required Guid CustomerId { get; init; }

    public required NotificationKind Kind { get; init; }

    public required string Title { get; set; }

    public required string Body { get; set; }

    public bool IsRead { get; private set; }

    /// <summary>Where tapping it goes — a storefront path, never an absolute URL.</summary>
    public string? Href { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public void MarkRead() => IsRead = true;
}

/// <summary>Whether a wallet movement settled — the frontend's <c>WalletTransaction.status</c>.</summary>
public enum WalletTransactionStatus
{
    Pending,
    Success,
    Failed,
}

/// <summary>
/// One movement of the customer's wallet — screen 58.
/// </summary>
/// <remarks>
/// <see cref="Amount"/> is a signed <see cref="long"/> rather than
/// <see cref="Money"/> precisely because the frontend's contract says
/// "positive credits the wallet, negative debits it" — and <see cref="Money"/>
/// refuses to be negative by construction. The running balance on
/// <see cref="Customer"/> is the value object; the ledger line is the signed
/// delta that produced it.
/// </remarks>
public sealed class WalletTransaction : Entity
{
    public required Guid CustomerId { get; init; }

    public required string Title { get; set; }

    /// <summary>Signed Toman: positive credits, negative debits.</summary>
    public required long Amount { get; init; }

    public WalletTransactionStatus Status { get; set; } = WalletTransactionStatus.Success;

    /// <summary>Material Symbols name drawn next to the row.</summary>
    public string Icon { get; set; } = "account_balance_wallet";

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A coupon issued to one customer — screen 59's list.
/// </summary>
/// <remarks>
/// Separate from <see cref="Orders.Coupon"/>, which is the code itself: the
/// same code can be granted to many customers, and "used" is per grant, not
/// per code.
/// </remarks>
public sealed class CouponGrant : Entity
{
    public required Guid CustomerId { get; init; }

    public required Guid CouponId { get; init; }

    /// <summary>Human-facing name of the offer, e.g. "۱۰٪ تخفیف اولین خرید".</summary>
    public required string Title { get; set; }

    /// <summary>The condition line under the title — display copy, not a rule the API enforces.</summary>
    public string Condition { get; set; } = string.Empty;

    public bool IsUsed { get; private set; }

    public DateTimeOffset GrantedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public void MarkUsed() => IsUsed = true;
}
