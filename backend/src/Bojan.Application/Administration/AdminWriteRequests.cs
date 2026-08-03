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

// --- product detail screens (106, 107, 108) --------------------------------

/// <remarks>
/// Each of the three posts the product's whole list, not a delta. The screens
/// edit a table in place — add a row, change one, delete one — and there is no
/// point in the flow where a single row is the unit of work. Replacing
/// wholesale also makes a deletion a deletion, which a stream of upserts cannot
/// express.
/// </remarks>
public sealed record VariantOptionRequest(string Key, string Label, string? Hex, bool? Available);

public sealed record VariantAxisRequest(
    string Key,
    string Label,
    string? Kind,
    IReadOnlyList<VariantOptionRequest> Options);

public sealed record SaveVariantsRequest(string Id, IReadOnlyList<VariantAxisRequest> Axes);

public sealed record SkuRequest(
    string Code,
    string? Barcode,
    string? Combination,
    long? Price,
    int? Stock,
    bool? Active);

public sealed record SaveSkusRequest(string Id, IReadOnlyList<SkuRequest> Skus);

public sealed record AttributeRequest(
    string Name,
    string? Kind,
    IReadOnlyList<string>? Values,
    bool? Filterable);

public sealed record SaveAttributesRequest(string Id, IReadOnlyList<AttributeRequest> Attributes);

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

/// <summary>One cell of screen 146's grid, as the panel's save button sends the whole matrix.</summary>
public sealed record RoleGrantRequest(string Role, string Section, bool Granted);

/// <summary><c>POST /admin/roles/permissions</c>'s body — the whole matrix, saved as one replace.</summary>
public sealed record RoleGrantsBody(IReadOnlyList<RoleGrantRequest> Grants);

public sealed record ApiKeyRequest(string? Id, string? Label, string? Scope, bool? Revoked);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record TwoFactorRequest(string Code, string? Secret);

/// <summary>
/// An operator's decision on a card-to-card top-up.
/// </summary>
/// <remarks>
/// The customer is not a field here, and neither is the amount: both are read
/// from the stored request. A decision endpoint that took an amount would be a
/// way to credit a wallet with a number of the caller's choosing.
/// </remarks>
public sealed record WalletTopUpDecisionRequest(string Id, bool Approve, string? Note);

/// <summary>
/// An operator cancelling an order.
/// </summary>
/// <remarks>
/// Neither the refund nor the penalty is a field: both are derived from the
/// order's own recorded figures and the stage it reached. A cancel endpoint
/// that took an amount would be a way to pay a wallet whatever the caller
/// fancied, which is the same reason
/// <see cref="WalletTopUpDecisionRequest"/> carries no amount either.
/// <para>
/// <c>ChargePenalty</c> defaults to true — the customer asked. The operator
/// clears it when the shop is at fault (out of stock after confirmation, a
/// pricing error), because the percentage is meant to cover work the customer's
/// change of mind wasted, not the shop's own.
/// </para>
/// </remarks>
public sealed record OrderCancellationRequest(string Id, string? Reason, bool ChargePenalty = true);
