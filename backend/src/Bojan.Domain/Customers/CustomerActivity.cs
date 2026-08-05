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

    /// <summary>
    /// The broadcast this row came from, or null for a notification raised by
    /// something that happened to this customer alone.
    /// </summary>
    /// <remarks>
    /// Carried so a fan-out can be resumed. Delivering a broadcast writes one
    /// row per customer in batches, and a batch that fails leaves the earlier
    /// ones committed with the campaign still unstamped — so the retry has to
    /// be able to tell who already has it. Without this, the retry re-sent the
    /// same offer to everyone the first attempt had reached.
    /// </remarks>
    public Guid? CampaignId { get; init; }

    public required string Title { get; set; }

    public required string Body { get; set; }

    public bool IsRead { get; private set; }

    /// <summary>
    /// Where tapping it goes — a storefront path, never an absolute URL.
    /// </summary>
    /// <remarks>
    /// Set through <see cref="WithLink"/> rather than assigned. This was a
    /// comment and nothing else: every caller so far builds the path itself, so
    /// the rule held by luck rather than by anything checking it. The moment an
    /// operator can type one — a targeted notification carries a link — that
    /// becomes a stored redirect shipped to a customer's inbox, and to every
    /// customer at once on a broadcast.
    /// </remarks>
    public string? Href { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public void MarkRead() => IsRead = true;

    /// <summary>
    /// Attaches a destination, rejecting anything that leaves the site.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The link is not a site-relative path. Callers taking one from an
    /// operator should test it with <see cref="IsInternalPath"/> and answer the
    /// request rather than let this surface as a fault.
    /// </exception>
    public CustomerNotification WithLink(string? href)
    {
        var trimmed = href?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            Href = null;
            return this;
        }

        if (!IsInternalPath(trimmed))
        {
            throw new ArgumentException($"A notification link must be a site-relative path: '{trimmed}'.", nameof(href));
        }

        Href = trimmed;
        return this;
    }

    /// <summary>
    /// Whether <paramref name="href"/> is a path within this site.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An allow-list of one shape — starts with a single <c>/</c> — rather than
    /// a list of schemes to block. <c>javascript:</c> and <c>data:</c> are the
    /// two everyone thinks of, and the browser knows dozens more.
    /// </para>
    /// <para>
    /// The three cases a leading-slash check alone still lets through:
    /// <c>//evil.example</c> is protocol-relative and leaves the site entirely
    /// while looking like a path; <c>/\evil.example</c> is treated as the same
    /// thing by browsers that normalise a backslash to a slash; and a control
    /// character can hide any of it from a human reading the value back. All
    /// three are refused.
    /// </para>
    /// </remarks>
    public static bool IsInternalPath(string? href) =>
        href is not null
        && href.Length > 1
        && href[0] == '/'
        && href[1] != '/'
        && href[1] != '\\'
        && !href.Contains('\\', StringComparison.Ordinal)
        && !href.Any(char.IsControl);
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
