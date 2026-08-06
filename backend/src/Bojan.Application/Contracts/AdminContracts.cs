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
    IReadOnlyList<string>? Images = null,
    /// <summary>
    /// The rest of what the product form collects.
    /// </summary>
    /// <remarks>
    /// Same reason as <see cref="Images"/>: the form posts every one of these,
    /// so it has to be able to read every one of them back, or editing a
    /// product silently clears whatever the form could not show. All defaulted,
    /// because the list projection loads none of them.
    /// </remarks>
    string? Slug = null,
    long? CompareAt = null,
    int LowStock = 5,
    bool TrackStock = true,
    bool Backorder = false,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? Description = null);

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
    string Status,
    /// <summary>
    /// The rest of what the category form collects, so the form can show what
    /// is stored rather than a blank field over a saved value.
    /// </summary>
    string? MetaTitle = null,
    string? MetaDescription = null,
    bool ShowInMenu = true,
    int Order = 0);

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
    string Status,
    string? Country = null,
    string? MetaTitle = null,
    string? MetaDescription = null);

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

/// <summary>
/// A card-to-card top-up in the review queue.
/// </summary>
/// <remarks>
/// Carries what the operator needs to match the transfer against a bank
/// statement — who, how much, which tracking number, and on what day — and
/// nothing they could use to change it. The decision endpoint reads the amount
/// from the stored request, not from this.
/// </remarks>
public sealed record AdminWalletTopUpDto(
    string Id,
    string CustomerId,
    string CustomerName,
    string CustomerPhone,
    long Amount,
    string Method,
    string Status,
    string? TrackingNumber,
    DateOnly? PaidOn,
    string? ReceiptUrl,
    string? CustomerNote,
    DateTimeOffset CreatedAt);

/// <summary>One product on a return request, as the queue and the detail screen draw it.</summary>
public sealed record AdminReturnItemDto(
    string ProductId,
    string Slug,
    string Title,
    string Image,
    int Quantity,
    /// <summary>Priced from the order's own frozen line price — see <c>ReturnRefund</c>.</summary>
    long UnitPrice);

/// <summary>
/// A return request in the operator's queue.
/// </summary>
/// <remarks>
/// Carries what an operator needs to judge it — who, against which order, what
/// is coming back and what it is worth — plus the two figures they cannot work
/// out from the screen. <see cref="RefundEstimate"/> is what the request would
/// pay if approved now, computed by the same code that will pay it, so the
/// number quoted before the decision and the number paid by it cannot disagree.
/// <see cref="Payable"/> is false when the order was never settled, which is a
/// refusal the operator should see before they click rather than after.
/// </remarks>
public sealed record AdminReturnDto(
    string Id,
    string Code,
    string OrderId,
    string OrderNumber,
    string CustomerId,
    string CustomerName,
    string CustomerPhone,
    string Status,
    string Reason,
    string? Description,
    string RefundMethod,
    long RefundEstimate,
    /// <summary>What was actually paid back. Zero until the request is refunded.</summary>
    long RefundAmount,
    bool Payable,
    bool Restocked,
    string? ReviewNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RefundedAt,
    IReadOnlyList<AdminReturnItemDto> Items);

/// <summary>
/// What deciding a return actually did, reported back so the panel can say so.
/// </summary>
/// <remarks>
/// Same purpose as <see cref="OrderCancellationDto"/>: the operator confirming a
/// decision cannot see its consequences from the screen, and both of the things
/// that might still need a person — money the shop owes by hand, and whether the
/// goods went back on the shelf — depend on rules they should not have to know.
/// </remarks>
/// <param name="RefundedToWallet">Credited to the wallet. Zero for a card refund.</param>
/// <param name="ManualRefund">
/// Owed and still to be paid by hand at the bank. No adapter behind
/// <c>IPaymentGateway</c> can reverse a card charge, so this is a figure for a
/// person, not a payment that has been made.
/// </param>
public sealed record ReturnDecisionDto(
    string Code,
    string Status,
    long RefundedToWallet,
    long ManualRefund,
    bool Restocked);

/// <summary>
/// What a cancellation actually did, reported back so the panel can say so.
/// </summary>
/// <remarks>
/// The operator confirming a cancellation cannot see the consequences from the
/// order screen: whether the goods went back on the shelf, and whether anything
/// is still owed the customer through the gateway, both depend on how far the
/// order had got. Returning them means the confirmation names the two things
/// that might still need a person, rather than leaving the operator to know the
/// rules.
/// </remarks>
/// <param name="Refunded">Credited to the wallet, after any penalty.</param>
/// <param name="Penalty">Withheld. Zero when the shop cancelled, or before the warehouse step.</param>
/// <param name="Restocked">False once dispatched — the goods are with a carrier and come back by hand.</param>
/// <param name="ManualGatewayRefund">Collected online and still to be returned by hand. Zero for a wallet-only or cash order.</param>
public sealed record OrderCancellationDto(
    string Number,
    long Refunded,
    long Penalty,
    bool Restocked,
    long ManualGatewayRefund);

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

/// <summary>
/// The dashboard's server-status card: the process and host the API is
/// actually running on right now, not a dependency list.
/// </summary>
/// <remarks>
/// <c>CpuLoadPercent</c> is sampled at call time over a short window rather
/// than read from a counter that needs a platform-specific provider, so it
/// costs this request a brief delay but works the same on every host the API
/// ships to. <c>null</c> when a single sample isn't meaningful (uptime under
/// the sampling window).
/// </remarks>
public sealed record ServerStatusDto(
    string Environment,
    string DotnetVersion,
    string OperatingSystem,
    long UptimeSeconds,
    long WorkingSetBytes,
    int ThreadCount,
    int ProcessorCount,
    double? CpuLoadPercent,
    long? TotalDiskBytes,
    long? FreeDiskBytes,
    bool DatabaseHealthy);

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
/// <remarks>
/// <c>Downloadable</c>, not the archive's location — the file lives behind
/// <c>GET /admin/backups/{id}/download</c>, authenticated the same as the
/// rest of this list, never at a URL the panel could link to directly.
/// </remarks>
public sealed record BackupJobDto(
    string Id,
    string Kind,
    string Status,
    bool Downloadable,
    long? SizeBytes,
    string? Error,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt);
