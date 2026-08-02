namespace Bojan.Application.Contracts;

/// <summary>
/// The panel's DTOs, mirroring <c>apps/admin/src/lib/types.ts</c>.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="ProductDto"/> and friends for the
/// reason that file gives: "the admin sees fields a customer never does (cost
/// price, stock movements, audit trails)". <see cref="AdminProductDto.CostPrice"/>
/// is the sharp edge — <c>BACKEND.md</c> Phase 7 says it "must never appear in
/// a storefront response", and keeping the two shapes in different types is
/// what makes that a compile-time fact rather than a review comment.
/// </remarks>
public sealed record AdminOrderItemDto(string Title, string Sku, int Quantity, long UnitPrice);

public sealed record AdminOrderDto(
    string Id,
    string Number,
    string Customer,
    string CustomerPhone,
    DateTimeOffset PlacedAt,
    string Status,
    int ItemCount,
    long Total,
    string PaymentMethod,
    string ShippingMethod,
    string Address,
    IReadOnlyList<AdminOrderItemDto> Items,
    /// <summary>What the shopper asked for on screen 74 — a preference an operator packing the order needs to see.</summary>
    string? DeliveryWindow = null);

public sealed record AdminProductDto(
    string Id,
    string Sku,
    string Title,
    string Brand,
    /// <summary>What <c>POST /products</c> expects back in its own <c>brand</c> field — see <see cref="AdminCatalogueService"/>.</summary>
    string BrandSlug,
    string Category,
    string CategorySlug,
    long Price,
    long CostPrice,
    int Stock,
    string Status,
    string Image,
    DateTimeOffset UpdatedAt,
    /// <summary>
    /// The whole gallery, primary image first — the order
    /// <c>POST /products</c> reads back from its own <c>images</c> field.
    /// </summary>
    /// <remarks>
    /// The write side has always accepted a list while the read side returned
    /// only <see cref="Image"/>, so screen 105 had no way to show what a
    /// product's images actually were. Defaulted so the list projection, which
    /// has no reason to load a gallery per row, can leave it out.
    /// </remarks>
    IReadOnlyList<string>? Images = null);

// --- product detail screens (106, 107, 108) --------------------------------

/// <summary>One option on an axis — screen 107.</summary>
public sealed record AdminVariantOptionDto(string Key, string Label, string? Hex, bool Available);

/// <summary>
/// One axis and its options — screen 107, and the same shape
/// <c>POST /products/variants</c> takes back.
/// </summary>
/// <remarks>
/// <c>Kind</c> is the lowercase spelling the frontend's own union uses
/// (<c>swatch</c> or <c>chip</c>), matching how every other enum crosses this
/// boundary.
/// </remarks>
public sealed record AdminVariantAxisDto(
    string Key,
    string Label,
    string Kind,
    IReadOnlyList<AdminVariantOptionDto> Options);

/// <summary>One sellable combination — screen 108.</summary>
public sealed record AdminSkuDto(
    string Id,
    string Code,
    string? Barcode,
    /// <summary>Option keys, one per axis, joined by <c>|</c> — e.g. <c>cream|a5</c>.</summary>
    string Combination,
    long Price,
    int Stock,
    bool Active);

/// <summary>One attribute and the values it may take — screen 106.</summary>
public sealed record AdminAttributeDto(
    string Id,
    string Name,
    /// <summary><c>text</c>, <c>number</c> or <c>boolean</c>.</summary>
    string Kind,
    IReadOnlyList<string> Values,
    bool Filterable);

/// <summary>
/// <c>Name</c> rather than <c>Title</c> is deliberate: <c>GET /categories</c>
/// doubles as the product form's category picker
/// (<c>apps/admin/src/lib/api/catalogue.ts</c>), which reads
/// <c>CatalogueOptionDto</c>'s <c>{ slug, name }</c> shape. Keeping the
/// property named <c>Name</c> here makes this DTO a structural superset of
/// that shape instead of a second, incompatible one.
/// </summary>
public sealed record AdminCategoryDto(
    string Id,
    string Name,
    string Slug,
    string Icon,
    string? Image,
    string? ParentId,
    string? ParentName,
    int ProductCount,
    string Status);

/// <summary>See <see cref="AdminCategoryDto"/>'s remarks — same reason <c>Name</c> and not <c>Title</c>.</summary>
public sealed record AdminBrandDto(
    string Id,
    string Name,
    string Slug,
    string? Tagline,
    string? Description,
    string? Logo,
    string? Cover,
    bool Featured,
    int ProductCount,
    string Status);

public sealed record AdminCollectionDto(
    string Id,
    string Title,
    string Slug,
    string? Summary,
    string? Cover,
    string? EditorialNote,
    bool Featured,
    int ProductCount,
    string Status);

public sealed record AdminCustomerDto(
    string Id,
    string Name,
    string Phone,
    string? Email,
    string Group,
    int OrderCount,
    long TotalSpent,
    DateTimeOffset JoinedAt,
    string Status);

public sealed record StockMovementDto(
    string Id,
    string Sku,
    string ProductTitle,
    string Kind,
    int Quantity,
    string Reason,
    DateTimeOffset At,
    string By);

/// <summary>One row of the inventory list — stock plus the thresholds screen 107 colours by.</summary>
public sealed record InventoryRowDto(
    string Id,
    string Sku,
    string Title,
    string Category,
    int Stock,
    int LowStockThreshold,
    DateTimeOffset UpdatedAt);

public sealed record AdminUserDto(
    string Id,
    string Name,
    string Email,
    string Role,
    DateTimeOffset? LastActiveAt,
    string Status);

public sealed record AuditEntryDto(string Id, string Actor, string Action, string Target, DateTimeOffset At, string Ip);

public sealed record CampaignDto(
    string Id,
    string Title,
    string Kind,
    string Status,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    int Reach,
    int Conversion,
    /// <summary>Only populated by <c>GetCampaignAsync</c> — the list screen has no use for it.</summary>
    string? Description = null);

public sealed record AdminCouponDto(
    string Id,
    string Code,
    string Title,
    int? Percent,
    long? Amount,
    int UsageLimit,
    int UsedCount,
    DateTimeOffset? ExpiresAt,
    bool Active,
    /// <summary>Only populated by <c>GetCouponAsync</c> — the list screen has no use for it.</summary>
    long? MinimumSpend);

public sealed record ContentEntryDto(
    string Id,
    string Title,
    string Type,
    string Status,
    string Author,
    DateTimeOffset UpdatedAt,
    /// <summary>Only populated by <c>GetContentAsync</c> — the list screen has no use for these.</summary>
    string? Slug = null,
    string? Excerpt = null,
    string? Body = null,
    string? Cover = null);

public sealed record SupportThreadDto(
    string Id,
    string Subject,
    string Customer,
    string Status,
    string Priority,
    DateTimeOffset UpdatedAt,
    int MessageCount);

public sealed record SupportThreadMessageDto(string Id, string Body, bool FromSupport, DateTimeOffset SentAt);

public sealed record SupportThreadDetailDto(
    string Id,
    string Subject,
    string Customer,
    string CustomerPhone,
    string? CustomerEmail,
    string Status,
    string Priority,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SupportThreadMessageDto> Messages);

public sealed record CannedReplyDto(string Id, string Title, string Body, DateTimeOffset UpdatedAt);

public sealed record AdminBusinessRequestDto(
    string Id,
    string Code,
    string Title,
    string Kind,
    string Status,
    string Organization,
    string Contact,
    string Phone,
    string? Email,
    int ItemCount,
    string? AssigneeId,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record ApiKeyDto(string Id, string Label, string Prefix, string Scope, bool Revoked, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);

/// <summary>Returned once, at creation. The plaintext key never appears again — only its hash is stored.</summary>
public sealed record CreatedApiKeyDto(string Id, string Label, string Prefix, string Scope, string Key);

/// <summary>
/// One dependency's health — screen 157.
/// </summary>
/// <remarks>
/// <c>Status</c> is <c>operational</c>, <c>degraded</c> or <c>down</c>, the
/// three the panel's <c>healthMeta</c> renders, derived from the health check's
/// own result. <c>CheckedAt</c> is when the check ran, because a stale "last
/// checked" on a status board is worse than none.
/// </remarks>
public sealed record ServiceHealthDto(
    string Id,
    string Name,
    string Status,
    int LatencyMs,
    DateTimeOffset CheckedAt,
    /// <summary>What failed, when something did. Absent for a healthy check.</summary>
    string? Detail = null);

// ---------------------------------------------------------------------------
// Dashboard and reports — screens 92 and 133-140.
//
// BACKEND.md Phase 6: "Push these into SQL — do not fetch rows and sum them in
// C#." Every aggregate below is the shape of one grouped query's result, not a
// list the API is expected to reduce afterwards.
// ---------------------------------------------------------------------------

public sealed record DashboardKpisDto(
    long RevenueToday,
    long RevenueThisMonth,
    int OrdersToday,
    int OrdersThisMonth,
    int PendingOrders,
    int LowStockProducts,
    int NewCustomersThisMonth,
    int OpenSupportThreads);

public sealed record SalesPointDto(DateTimeOffset Period, long Revenue, int Orders);

public sealed record StatusCountDto(string Status, int Count);

public sealed record TopProductDto(string ProductId, string Title, string Sku, int UnitsSold, long Revenue);

public sealed record CustomerGrowthPointDto(DateTimeOffset Period, int NewCustomers, int ReturningCustomers);

public sealed record CampaignPerformanceDto(string CampaignId, string Title, int Reach, int Conversion, double ConversionRate);

public sealed record FinancialTotalsDto(
    long GrossRevenue,
    long Discounts,
    long Shipping,
    long NetRevenue,
    long CostOfGoods,
    long GrossProfit,
    int OrderCount,
    /// <summary>
    /// The same revenue split by how it was paid — screen 139's table.
    /// </summary>
    /// <remarks>
    /// Aggregated here rather than on the panel, which was grouping a capped
    /// page of orders while showing totals that covered every order, so the
    /// table did not sum to the figure printed above it. Grouped by the stored
    /// method title, so a method the catalogue adds later appears without any
    /// code change.
    /// </remarks>
    IReadOnlyList<PaymentMethodTotalDto>? ByPaymentMethod = null);

public sealed record PaymentMethodTotalDto(string Method, int Count, long Amount);

public sealed record StockLevelsDto(
    int InStock,
    int LowStock,
    int OutOfStock,
    long InventoryValue,
    /// <summary>
    /// Units on hand across the catalogue — the inventory report's headline,
    /// which it used to reach by summing whatever products fitted on one page.
    /// </summary>
    int TotalUnits = 0);

/// <summary>
/// Catalogue-wide counts — screen 137.
/// </summary>
/// <remarks>
/// Counted in the database rather than by the panel over a capped page. A store
/// with more products than fit on a page was reporting the page size as its
/// catalogue size.
/// </remarks>
public sealed record CatalogueSummaryDto(int Total, int Published, int Draft, int Archived, int OutOfStock);

/// <summary>
/// Customer-base totals — screen 138, for the same reason.
/// </summary>
public sealed record CustomerSummaryDto(int Total, int Business, int Blocked, long TotalSpend);

/// <summary>One granted role×section cell — screen 146's grid.</summary>
public sealed record RolePermissionDto(string Role, string Section);

/// <summary>A queued or completed backup job — screen 156's table.</summary>
public sealed record BackupJobDto(
    string Id,
    string Kind,
    string Status,
    string? FileUrl,
    long? SizeBytes,
    string? Error,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt);
