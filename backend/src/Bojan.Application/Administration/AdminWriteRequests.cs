namespace Bojan.Application.Administration;

/// <summary>
/// One record per row of <c>BACKEND.md</c>'s Phase 7 table, carrying exactly
/// the fields that row lists.
/// </summary>
/// <remarks>
/// <para>
/// The panel's own allow-list
/// (<c>apps/admin/src/lib/api/resources.ts</c>) already drops anything not on
/// these lists before forwarding. Declaring them again as types is the second
/// half of that guarantee: a field the panel never sends has nowhere to land
/// here either, so a request that bypasses the panel entirely cannot set
/// <c>costPrice</c> through a form that does not show it.
/// </para>
/// <para>
/// Ids arrive as strings because that is how the panel holds them, and are
/// parsed at the edge of each handler — a malformed id is a 400, not an
/// exception in the model binder.
/// </para>
/// </remarks>
public sealed record SaveProductRequest(
    string? Id,
    string? Title,
    string? Sku,
    string? Brand,
    string? Category,
    long? Price,
    long? CostPrice,
    int? Stock,
    string? Status,
    string? Description,
    IReadOnlyList<string>? Images);

public sealed record ProductPricingRequest(string Id, long? Price, long? CostPrice, long? CompareAtPrice);

public sealed record ProductDiscountRequest(
    string Id,
    int? Percent,
    long? Amount,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

public sealed record SaveCategoryRequest(
    string? Id,
    string? Title,
    string? Slug,
    string? ParentId,
    string? Description,
    string? Icon,
    string? Status);

public sealed record SaveBrandRequest(
    string? Id,
    string? Title,
    string? Slug,
    string? Description,
    string? Logo,
    string? Status);

public sealed record SaveCollectionRequest(
    string? Id,
    string? Title,
    string? Slug,
    string? Description,
    string? Cover,
    string? Status);

public sealed record SaveContentRequest(
    string? Id,
    string? Title,
    string? Slug,
    string? Kind,
    string? Body,
    string? Excerpt,
    string? Cover,
    string? Status);

public sealed record SaveCampaignRequest(
    string? Id,
    string? Title,
    string? Kind,
    string? Status,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    string? Description);

public sealed record SaveCouponRequest(
    string? Id,
    string? Code,
    int? Percent,
    long? Amount,
    long? MinimumSpend,
    DateTimeOffset? ExpiresAt,
    string? Status);

public sealed record StockMovementRequest(string ProductId, string Kind, int Quantity, string Reason, string? Reference);

public sealed record OrderStatusRequest(string Id, string Status, string? Note, string? TrackingCode);

public sealed record BusinessRequestUpdate(string Id, string? Status, string? AssigneeId, string? Note);

public sealed record SupportReplyRequest(string ThreadId, string Body);

public sealed record CannedReplyRequest(string? Id, string? Title, string? Body, bool? Deleted);

public sealed record BroadcastRequest(string Channel, string Audience, string Title, string Body, DateTimeOffset? ScheduledAt);

public sealed record ReportExportRequest(string Report, string? Format, DateTimeOffset? From, DateTimeOffset? To);

/// <summary><c>values</c> is an arbitrary JSON object; the section decides what is in it.</summary>
public sealed record SettingsRequest(string Section, IReadOnlyDictionary<string, string> Values);

public sealed record BackupRequest(string Kind, bool Confirm);

public sealed record ApiKeyRequest(string? Id, string? Label, string? Scope, bool? Revoked);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record TwoFactorRequest(string Code, string? Secret);
