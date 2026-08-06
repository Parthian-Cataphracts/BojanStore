using Bojan.Domain.Business;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Content;
using Bojan.Domain.Customers;
using Bojan.Domain.Inventory;
using Bojan.Domain.Marketing;
using Bojan.Domain.Orders;
using Bojan.Domain.Reviews;
using Bojan.Domain.Support;

namespace Bojan.Application.Common;

/// <summary>
/// Converts domain enums to the exact strings the frontend's union types
/// declare.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these is a lowercase spelling of the enum name, which is why
/// they are written as a single helper rather than a switch per call site. The
/// two exceptions are the interesting ones, and both are documented at the
/// method that makes them:
/// </para>
/// <list type="bullet">
/// <item><see cref="StorefrontOrderStatus"/> collapses
/// <see cref="OrderStatus.Packed"/> onto <c>processing</c>, because the
/// storefront's <c>OrderStatus</c> union has no seventh value.</item>
/// <item><see cref="ProductStatus"/> is not an enum at all — a product's
/// published/draft/archived state is derived from two booleans.</item>
/// </list>
/// </remarks>
public static class WireFormat
{
    private static string Lower<T>(T value) where T : struct, Enum =>
        value.ToString().ToLowerInvariant();

    /// <summary>
    /// The storefront's six-value <c>OrderStatus</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="OrderStatus.Packed"/> becomes <c>processing</c>: the panel
    /// tracks "picked and boxed, not yet with a carrier" as its own step, and
    /// the customer-facing union
    /// (<c>apps/storefront/src/lib/api/types.ts</c>) does not have it. Emitting
    /// <c>packed</c> here would give the order screens a status their
    /// <c>orderStatusMeta</c> lookup has no entry for, which renders as a
    /// missing badge rather than an error.
    /// </remarks>
    public static string StorefrontOrderStatus(OrderStatus status) =>
        status == OrderStatus.Packed ? "processing" : Lower(status);

    /// <summary>The panel's seven-value <c>AdminOrderStatus</c>, which does include <c>packed</c>.</summary>
    public static string AdminOrderStatus(OrderStatus status) => Lower(status);

    /// <summary>Parses either vocabulary back, for a <c>?status=</c> filter. Null when the value is not a status.</summary>
    public static OrderStatus? ParseOrderStatus(string? value) =>
        Enum.TryParse<OrderStatus>(value, ignoreCase: true, out var parsed) ? parsed : null;

    public static string ReturnStatus(ReturnStatus status) => Lower(status);

    /// <summary>Parses one back, for the decision endpoint and the <c>?status=</c> filter. Null when it is not a status.</summary>
    public static ReturnStatus? ParseReturnStatus(string? value) =>
        Enum.TryParse<ReturnStatus>(value, ignoreCase: true, out var parsed) ? parsed : null;

    public static string NotificationKind(NotificationKind kind) => Lower(kind);

    public static string WalletStatus(WalletTransactionStatus status) => Lower(status);

    public static string ReviewStatus(ModerationStatus status) => Lower(status);

    public static string TicketStatus(SupportTicketStatus status) => Lower(status);

    public static string TicketPriority(SupportPriority priority) => Lower(priority);

    public static string BusinessRequestKind(BusinessRequestKind kind) => Lower(kind);

    public static string BusinessRequestStatus(BusinessRequestStatus status) => Lower(status);

    public static string QuoteStatus(QuoteStatus status) => Lower(status);

    public static string CampaignKind(CampaignKind kind) => Lower(kind);

    public static string CampaignStatus(CampaignStatus status) => Lower(status);

    public static string ContentKind(ContentKind kind) => Lower(kind);

    public static string ContentStatus(ContentStatus status) => Lower(status);

    public static string StockMovementKind(StockMovementKind kind) => Lower(kind);

    public static string ArticleBlockKind(ArticleBlockKind kind) => Lower(kind);

    public static string VariantAxisKind(VariantAxisKind kind) => Lower(kind);

    /// <summary>
    /// The panel's <c>AdminProduct.status</c> —
    /// <c>'published' | 'draft' | 'archived'</c>.
    /// </summary>
    /// <remarks>
    /// Not stored as a field. Archived is the soft-delete flag every catalogue
    /// entity already carries (<c>BACKEND.md</c> section 1.2: "the panel's
    /// delete actions should not lose history"), and published/draft is
    /// <c>IsPublished</c>. Deriving it here keeps one source of truth rather
    /// than a status column that can disagree with the two flags beside it.
    /// </remarks>
    public static string ProductStatus(bool isPublished, bool isDeleted) =>
        isDeleted ? "archived" : isPublished ? "published" : "draft";

    public static string CustomerStatus(bool isBlocked) => isBlocked ? "blocked" : "active";

    public static string AdminUserStatus(bool isActive) => isActive ? "active" : "suspended";

    /// <summary>
    /// Where a timeline step sits relative to where the order actually got to:
    /// <c>done</c> and <c>current</c> render filled, <c>upcoming</c> hollow.
    /// </summary>
    public static string TimelineState(int stepIndex, int reachedIndex) =>
        stepIndex < reachedIndex ? "done" : stepIndex == reachedIndex ? "current" : "upcoming";
}
