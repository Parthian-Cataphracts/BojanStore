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
    string? DeliveryWindow = null,
    /// <summary>
    /// Whether the money for this order has actually been collected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It was not on this DTO at all, which meant the panel could not say
    /// whether an order had been paid for and had no control to record that it
    /// had. <c>Order.PaymentStatus</c> existed, was migrated, was guarded
    /// against shipping an unpaid order and was covered by domain tests — and
    /// nothing an operator could see or press ever read or wrote it.
    /// </para>
    /// <para>
    /// The wire values are the enum's own names, lowercased by
    /// <c>WireFormat</c> like every other status this API sends.
    /// </para>
    /// </remarks>
    string PaymentStatus = "awaiting-payment",
    DateTimeOffset? PaidAt = null,
    /// <summary>
    /// The gateway's reference, or whatever the operator typed when settling by
    /// hand — a transfer's tracking number, usually.
    /// </summary>
    string? PaymentReference = null);

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
    /// because the list projection loads none of them — except
    /// <see cref="Slug"/>, which is a plain column and is how every catalogue
    /// write names a product.
    /// </remarks>
    string? Slug = null,
    long? CompareAt = null,
    int LowStock = 5,
    bool TrackStock = true,
    bool Backorder = false,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? Description = null,
    /// <summary>
    /// Every category the product is filed under, primary first — the same
    /// order <c>POST /products</c> reads back in its own <c>categories</c>
    /// field.
    /// </summary>
    /// <remarks>
    /// <see cref="CategorySlug"/> stays alongside it and is the first of these:
    /// the list screen shows one category per row, and the storefront's
    /// breadcrumb has room for one. Defaulted for the list projection, which
    /// loads neither this nor the collections below.
    /// </remarks>
    IReadOnlyList<string>? CategorySlugs = null,
    /// <summary>The collections this product belongs to, by slug.</summary>
    IReadOnlyList<string>? CollectionSlugs = null);

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
    bool Active,
    /// <summary>This combination's own list price when it is on sale; null when it is not.</summary>
    long? CompareAt = null);

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
    string Status,
    /// <summary>
    /// What the collection holds, in the order an operator arranged it.
    /// </summary>
    /// <remarks>
    /// Defaulted, because the list projection has no reason to load a
    /// membership list per row — it already carries
    /// <see cref="ProductCount"/>, which is all that screen shows.
    /// </remarks>
    IReadOnlyList<string>? ProductSlugs = null);

/// <summary>
/// One account of any kind — a shopper or an operator — for the panel's single
/// users list.
/// </summary>
/// <remarks>
/// <para>
/// The two live in separate tables and always will: this row is named by orders
/// and that one by audit entries, and neither set of references moves without a
/// migration. What the panel needed was not one table but one list, because
/// "who has an account here" is a single question and answering it from two
/// screens meant an operator who also shops appeared twice with no sign the two
/// were the same person.
/// </para>
/// <para>
/// <see cref="Kind"/> says which table the row came from, so a screen knows
/// which editor to open; <see cref="Role"/> is the answer people actually want
/// — <c>customer</c>, or the operator's own role.
/// </para>
/// </remarks>
public sealed record AdminAccountDto(
    string Id,
    string Name,
    string Phone,
    string? Email,
    /// <summary><c>customer</c> or <c>operator</c> — which table, and so which editor.</summary>
    string Kind,
    /// <summary><c>customer</c>, <c>owner</c>, <c>product</c>, <c>sales</c> or <c>support</c>.</summary>
    string Role,
    /// <summary><c>active</c>, <c>blocked</c> for a shopper, <c>suspended</c> for an operator.</summary>
    string Status,
    DateTimeOffset JoinedAt,
    /// <summary>The shop's reference for a shopper; empty for an operator, who has none.</summary>
    string Code = "",
    /// <summary>
    /// Set on an operator who also shops here, naming the customer row they
    /// order through — the one place the two halves of a person are joined.
    /// </summary>
    string? LinkedCustomerId = null);

/// <summary>
/// One customer, as the panel lists and edits them.
/// </summary>
/// <remarks>
/// The fields after <see cref="Status"/> are what the edit form needs and the
/// list has no use for — the detail query fills them, the list leaves them null.
/// <see cref="Name"/> stays a single rendered string because that is what the
/// table draws; the two halves travel separately for the form.
/// </remarks>
public sealed record AdminCustomerDto(
    string Id,
    string Name,
    string Phone,
    string? Email,
    string Group,
    int OrderCount,
    long TotalSpent,
    DateTimeOffset JoinedAt,
    string Status,
    /// <summary>The shop's own reference — <c>BZ-00042</c>.</summary>
    string Code = "",
    string? FirstName = null,
    string? LastName = null,
    string? City = null,
    string? NationalId = null,
    /// <summary>ISO <c>YYYY-MM-DD</c>.</summary>
    string? BirthDate = null,
    /// <summary>
    /// Whether this account can be removed outright, or has traded and must be
    /// suspended instead. Decided on the server so the panel does not have to
    /// guess at the same rule.
    /// </summary>
    bool? Deletable = null);

/// <summary>
/// One message the shop has sent, for the panel's «ارسال اعلان» screen.
/// </summary>
/// <remarks>
/// <para>
/// Two tables behind one list, the way the operator thinks about it: a
/// broadcast is a <c>NotificationCampaign</c> row, a message to one person is a
/// <c>CustomerNotification</c>. Merging them here rather than in the screen
/// means "what have we sent?" has one answer and one sort order.
/// </para>
/// <para>
/// <see cref="Recipient"/> is a name to show, not an id to act on —
/// «همه کاربران» for a broadcast, the customer's own name otherwise.
/// <see cref="Kind"/> is what the delete route needs, because the two live in
/// different tables and the id alone does not say which.
/// </para>
/// </remarks>
public sealed record AdminNotificationDto(
    string Id,
    /// <summary><c>broadcast</c> or <c>customer</c>.</summary>
    string Kind,
    string Title,
    string Body,
    string Recipient,
    string? Link,
    string SentAt);

/*
    The report rows an operator actually asked for.

    Every report here used to export its dashboard summary — six numbers for
    "sales", one row of totals for "customers" — which is the right shape for a
    chart and useless as a report. "چه چیزی فروخته شد، به چه کسی، کی، به چه
    قیمتی" was not answerable from any file the panel produced.

    These are the itemised versions: one row per thing that happened, with the
    names, dates, amounts and statuses spelled out. Column headers come from
    [ReportColumn] rather than from the property name, so the file an operator
    opens is in the language they read while the code stays ordinary C#.
*/

/// <summary>The Persian heading this property is written under.</summary>
/// <remarks>
/// Read by <c>CsvWriter</c> and <c>XlsxWriter</c>. Without it the header row is
/// the property name, which is how a report meant for a shopkeeper ended up
/// with a column called <c>UnitPrice</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ReportColumnAttribute(string header) : Attribute
{
    public string Header { get; } = header;
}

/// <summary>One line of one order: the finest grain the shop records a sale at.</summary>
public sealed record SalesDetailRow(
    [property: ReportColumn("شماره سفارش")] string OrderNumber,
    [property: ReportColumn("تاریخ")] string PlacedAt,
    [property: ReportColumn("مشتری")] string Customer,
    [property: ReportColumn("موبایل")] string Phone,
    [property: ReportColumn("کالا")] string Product,
    [property: ReportColumn("کد کالا")] string Sku,
    [property: ReportColumn("تعداد")] int Quantity,
    [property: ReportColumn("قیمت واحد (تومان)")] long UnitPrice,
    [property: ReportColumn("جمع ردیف (تومان)")] long LineTotal,
    [property: ReportColumn("وضعیت سفارش")] string OrderStatus,
    [property: ReportColumn("وضعیت پرداخت")] string PaymentStatus,
    [property: ReportColumn("روش ارسال")] string ShippingMethod,
    [property: ReportColumn("روش پرداخت")] string PaymentMethod);

/// <summary>One order, with its money broken out rather than summed away.</summary>
public sealed record OrdersDetailRow(
    [property: ReportColumn("شماره سفارش")] string OrderNumber,
    [property: ReportColumn("تاریخ")] string PlacedAt,
    [property: ReportColumn("مشتری")] string Customer,
    [property: ReportColumn("موبایل")] string Phone,
    [property: ReportColumn("تعداد اقلام")] int ItemCount,
    [property: ReportColumn("جمع کالاها (تومان)")] long Subtotal,
    [property: ReportColumn("تخفیف (تومان)")] long Discount,
    [property: ReportColumn("هزینه ارسال (تومان)")] long Shipping,
    [property: ReportColumn("مبلغ کل (تومان)")] long Total,
    [property: ReportColumn("وضعیت سفارش")] string OrderStatus,
    [property: ReportColumn("وضعیت پرداخت")] string PaymentStatus,
    [property: ReportColumn("کد تخفیف")] string Coupon,
    [property: ReportColumn("کد رهگیری")] string Tracking);

/// <summary>One customer, with what they have actually spent.</summary>
public sealed record CustomersDetailRow(
    [property: ReportColumn("شناسه")] string Code,
    [property: ReportColumn("نام")] string Name,
    [property: ReportColumn("موبایل")] string Phone,
    [property: ReportColumn("ایمیل")] string Email,
    [property: ReportColumn("شهر")] string City,
    [property: ReportColumn("گروه")] string Group,
    [property: ReportColumn("وضعیت")] string Status,
    [property: ReportColumn("تعداد سفارش")] int OrderCount,
    [property: ReportColumn("مجموع خرید (تومان)")] long TotalSpent,
    [property: ReportColumn("آخرین خرید")] string LastOrderAt,
    [property: ReportColumn("تاریخ عضویت")] string JoinedAt);

/// <summary>One sellable item, with its stock and price as they stand.</summary>
public sealed record InventoryDetailRow(
    [property: ReportColumn("کد کالا")] string Sku,
    [property: ReportColumn("کالا")] string Product,
    [property: ReportColumn("دسته")] string Category,
    [property: ReportColumn("برند")] string Brand,
    [property: ReportColumn("قیمت (تومان)")] long Price,
    [property: ReportColumn("موجودی")] int Stock,
    [property: ReportColumn("حد هشدار")] int LowStockThreshold,
    [property: ReportColumn("وضعیت")] string Status);

/// <summary>One payment, so a figure in the financial report can be traced to it.</summary>
public sealed record FinancialDetailRow(
    [property: ReportColumn("شماره سفارش")] string OrderNumber,
    [property: ReportColumn("تاریخ سفارش")] string PlacedAt,
    [property: ReportColumn("مشتری")] string Customer,
    [property: ReportColumn("مبلغ کل (تومان)")] long Total,
    [property: ReportColumn("پرداخت از کیف پول (تومان)")] long WalletPaid,
    [property: ReportColumn("پرداخت آنلاین (تومان)")] long OnlinePaid,
    [property: ReportColumn("روش پرداخت")] string PaymentMethod,
    [property: ReportColumn("وضعیت پرداخت")] string PaymentStatus,
    [property: ReportColumn("کد پیگیری")] string Reference,
    [property: ReportColumn("تاریخ پرداخت")] string PaidAt);

/// <summary>One campaign and what it reached.</summary>
public sealed record CampaignsDetailRow(
    [property: ReportColumn("عنوان")] string Title,
    [property: ReportColumn("کانال")] string Channel,
    [property: ReportColumn("مخاطب")] string Audience,
    [property: ReportColumn("تاریخ ساخت")] string CreatedAt,
    [property: ReportColumn("تاریخ ارسال")] string SentAt,
    [property: ReportColumn("ارسال شده")] int Sent,
    [property: ReportColumn("تحویل شده")] int Delivered,
    [property: ReportColumn("ناموفق")] int Failed);

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

/// <summary>
/// One row of screen 145.
/// </summary>
/// <remarks>
/// The fields after <see cref="Status"/> are what the screen needs to edit a
/// row rather than only draw it: the phone because it is the other sign-in
/// identity and the form has to show what is stored, and the two flags because
/// each has a control that must not be offered when it would do nothing —
/// clearing a second factor nobody has, or telling an owner to chase a password
/// change that has already happened.
///
/// No password field, in any shape. Nothing reads one back, including this.
/// </remarks>
public sealed record AdminUserDto(
    string Id,
    string Name,
    string Email,
    string Role,
    DateTimeOffset? LastActiveAt,
    string Status,
    string? Phone,
    bool TwoFactorEnabled,
    bool MustChangePassword,
    DateTimeOffset CreatedAt,
    /// <summary>
    /// The panel sections this operator has been narrowed to.
    /// </summary>
    /// <remarks>
    /// Empty means unnarrowed — the role's own reach — rather than «nothing»,
    /// which is the same rule the filter applies. The screen has to know the
    /// difference or it would draw an unrestricted operator as one locked out
    /// of everything.
    /// </remarks>
    IReadOnlyList<string>? Sections = null);

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

/// <summary>
/// One magazine article, as the panel lists and edits it.
/// </summary>
/// <remarks>
/// Separate from <see cref="ContentEntryDto"/> because the underlying tables
/// are separate and only one of them is the magazine. An article carries what
/// the article page renders — an editorial category, a reading time, a featured
/// flag — none of which a static page or an FAQ has.
/// </remarks>
public sealed record AdminArticleDto(
    string Id,
    string Slug,
    string Title,
    string Excerpt,
    string Category,
    string Cover,
    /// <summary><c>published</c>, <c>draft</c> or <c>archived</c>.</summary>
    string Status,
    bool Featured,
    int ReadingMinutes,
    DateTimeOffset PublishedAt,
    /// <summary>
    /// The body flattened back to the plain text the editor posts — blank line
    /// between paragraphs, <c>##</c> before a heading. Only the detail query
    /// fills it; the list has no use for it.
    /// </summary>
    string? Body = null);

/// <summary>
/// A customer review as the moderation queue lists it.
/// </summary>
/// <remarks>
/// Carries the product because the queue is cross-product — an operator
/// working through «در انتظار» is reading reviews of a dozen different things,
/// and a row that does not say what it is about cannot be judged at all.
/// <see cref="Status"/> is the storefront's own vocabulary
/// (<c>pending | published | rejected</c>) so the panel and the shop describe
/// the same review the same way.
/// </remarks>
/// <summary>
/// One product question in the panel's queue — «پرسش‌ها».
/// </summary>
/// <remarks>
/// The same shape the review queue answers with, minus the things a question
/// does not have (a rating, a home-page tick) and plus the answer itself. A
/// question is published by being answered, so the two travel together.
/// </remarks>
public sealed record AdminQuestionDto(
    string Id,
    string ProductId,
    string ProductSlug,
    string ProductTitle,
    string Author,
    string Body,
    string Status,
    string? Answer,
    string? AnswerAuthor,
    DateTimeOffset? AnsweredAt,
    DateTimeOffset AskedAt);

public sealed record AdminReviewDto(
    string Id,
    string ProductId,
    string ProductSlug,
    string ProductTitle,
    string Author,
    int Rating,
    string? Title,
    string Body,
    string Status,
    bool FeaturedOnHome,
    bool Verified,
    int HelpfulCount,
    DateTimeOffset CreatedAt);

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
