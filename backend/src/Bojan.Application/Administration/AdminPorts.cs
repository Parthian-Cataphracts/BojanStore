using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Admin;
using Bojan.Domain.Business;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
using Bojan.Domain.Content;
using Bojan.Domain.Customers;
using Bojan.Domain.Marketing;
using Bojan.Domain.Orders;

namespace Bojan.Application.Administration;

/// <summary>
/// The filters every panel table shares.
/// </summary>
/// <remarks>
/// <c>q</c>, <c>status</c> and <c>page</c> are the query-string keys the
/// screens already use (see <c>apps/admin/src/app/orders/page.tsx</c> and its
/// siblings), and <see cref="DefaultPageSize"/> is their <c>PAGE_SIZE</c>. The
/// panel has no read layer yet — <c>BACKEND.md</c> Phase 6 notes it "reads
/// fixtures directly" — so these names are what the mirroring frontend task
/// should send, not something already on the wire.
/// </remarks>
public sealed record AdminListQuery(
    string? Search = null,
    string? Status = null,
    string? Kind = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = AdminListQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    /// <summary>
    /// The deepest page this will seek to.
    /// </summary>
    /// <remarks>
    /// Only the floor was clamped, so <c>?page=20000000&amp;pageSize=200</c>
    /// multiplied out past <see cref="int.MaxValue"/>, wrapped negative, and
    /// went to the database as <c>OFFSET -294967296</c> — a 500 where the
    /// honest answer to a page past the end is an empty one. A hundred thousand
    /// pages is two million rows at the default size, deeper than any panel
    /// table will be paged through by hand, and small enough that the product
    /// stays inside an int at every allowed page size.
    /// </remarks>
    public const int MaxPage = 100_000;

    public AdminListQuery Normalised() => this with
    {
        Page = Math.Clamp(Page, 1, MaxPage),
        PageSize = PageSize is < 1 or > MaxPageSize ? DefaultPageSize : PageSize,
    };
}

/// <summary>How the reports group time — the period selector on screens 133-140.</summary>
public enum ReportGrouping
{
    Day,
    Week,
    Month,
}

/// <summary>
/// Everything the panel's lists, dashboard and reports read.
/// </summary>
/// <remarks>
/// The aggregate methods return one row per group. <c>BACKEND.md</c> Phase 6:
/// "Push these into SQL — do not fetch rows and sum them in C#." The
/// implementation is a <c>GroupBy</c> that EF translates, not a
/// <c>ToListAsync</c> followed by LINQ-to-objects.
/// </remarks>
public interface IAdminQueries
{
    Task<Paged<AdminOrderDto>> ListOrdersAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<AdminOrderDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// The issued invoices, newest first.
    /// </summary>
    /// <remarks>
    /// Every issued invoice belongs to a delivered order — the number is minted
    /// exactly at that transition — so an order that was cancelled, returned or
    /// never delivered simply is not in this list. <see cref="AdminListQuery.Search"/>
    /// matches the invoice number, the order number or the customer's name.
    /// </remarks>
    Task<Paged<InvoiceSummaryDto>> ListInvoicesAsync(AdminListQuery query, CancellationToken cancellationToken);

    /// <summary>The full invoice document for one order, or null when it has none.</summary>
    Task<InvoiceDto?> GetInvoiceAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Paged<AdminProductDto>> ListProductsAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<AdminProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Screen 107 — the product's axes and their options.</summary>
    Task<IReadOnlyList<AdminVariantAxisDto>> GetProductVariantsAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Screen 108 — the product's sellable combinations.</summary>
    Task<IReadOnlyList<AdminSkuDto>> GetProductSkusAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Screen 106 — the product's attributes.</summary>
    Task<IReadOnlyList<AdminAttributeDto>> GetProductAttributesAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>The product's B2B volume ladder, lowest rung first.</summary>
    Task<IReadOnlyList<ProductVolumeTierDto>> GetProductVolumeTiersAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>
    /// The sellable catalogue with each product's volume ladder attached, for the
    /// screen that composes a pro-forma.
    /// </summary>
    Task<IReadOnlyList<AdminQuotableProductDto>> ListQuotableProductsAsync(CancellationToken cancellationToken);

    Task<Paged<AdminCategoryDto>> ListCategoriesAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<AdminCategoryDto?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken);

    Task<Paged<AdminBrandDto>> ListBrandsAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<AdminBrandDto?> GetBrandAsync(Guid brandId, CancellationToken cancellationToken);

    Task<Paged<AdminCollectionDto>> ListCollectionsAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<AdminCollectionDto?> GetCollectionAsync(Guid collectionId, CancellationToken cancellationToken);

    Task<Paged<AdminCustomerDto>> ListCustomersAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<AdminCustomerDto?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    Task<Paged<InventoryRowDto>> ListInventoryAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<Paged<StockMovementDto>> ListStockMovementsAsync(AdminListQuery query, CancellationToken cancellationToken);

    /// <summary>The returns queue. Open requests first, then oldest first within each state.</summary>
    Task<Paged<AdminReturnDto>> ListReturnsAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<AdminReturnDto?> GetReturnAsync(Guid returnId, CancellationToken cancellationToken);

    Task<Paged<AdminBusinessRequestDto>> ListBusinessRequestsAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<Paged<AdminCouponDto>> ListCouponsAsync(AdminListQuery query, CancellationToken cancellationToken);
    Task<AdminCouponDto?> GetCouponAsync(Guid couponId, CancellationToken cancellationToken);

    Task<Paged<CampaignDto>> ListCampaignsAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<CampaignDto?> GetCampaignAsync(Guid campaignId, CancellationToken cancellationToken);

    Task<Paged<ContentEntryDto>> ListContentAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<ContentEntryDto?> GetContentAsync(Guid contentId, CancellationToken cancellationToken);

    Task<Paged<SupportThreadDto>> ListSupportThreadsAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<SupportThreadDetailDto?> GetSupportThreadAsync(Guid threadId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CannedReplyDto>> ListCannedRepliesAsync(CancellationToken cancellationToken);

    /// <summary>The card-to-card review queue. Pending first, then oldest first within each state.</summary>
    Task<Paged<AdminWalletTopUpDto>> ListWalletTopUpsAsync(
        AdminListQuery query,
        string? status,
        CancellationToken cancellationToken);

    Task<Paged<AuditEntryDto>> ListAuditAsync(AdminListQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Every account the shop has, shoppers and operators together — the panel's
    /// single users list.
    /// </summary>
    Task<Paged<AdminAccountDto>> ListAccountsAsync(AdminListQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Everything the shop has sent — broadcasts and one-to-one messages in one
    /// list, newest first.
    /// </summary>
    Task<Paged<AdminNotificationDto>> ListNotificationsAsync(AdminListQuery query, CancellationToken cancellationToken);

    /// <summary>Screen 122 — the magazine's articles, archived ones included.</summary>
    Task<Paged<AdminArticleDto>> ListAdminArticlesAsync(AdminListQuery query, CancellationToken cancellationToken);

    /// <summary>Screen 123 — one article for the editor, body as plain text.</summary>
    Task<AdminArticleDto?> GetAdminArticleAsync(Guid id, CancellationToken cancellationToken);

    Task<Paged<AdminUserDto>> ListAdminUsersAsync(AdminListQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// The operator's own name, for a document that has to say who issued it.
    /// </summary>
    /// <remarks>
    /// Read from their record rather than taken from the request: a pro-forma
    /// names a sales rep, and that is not a field a caller should be able to
    /// choose. Falls back to their email, and then to a placeholder, because a
    /// quote that cannot be issued over a missing display name would be a
    /// strange thing to refuse.
    /// </remarks>
    Task<string> GetAdminDisplayNameAsync(Guid adminId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiKeyDto>> ListApiKeysAsync(CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>> GetSettingsAsync(string section, CancellationToken cancellationToken);

    // --- aggregates -------------------------------------------------------

    Task<DashboardKpisDto> GetDashboardKpisAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SalesPointDto>> GetSalesAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, ReportGrouping grouping, CancellationToken cancellationToken);

    Task<IReadOnlyList<StatusCountDto>> GetOrderStatusCountsAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerGrowthPointDto>> GetCustomerGrowthAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, ReportGrouping grouping, CancellationToken cancellationToken);

    Task<StockLevelsDto> GetStockLevelsAsync(CancellationToken cancellationToken);

    /// <summary>Screen 137 — catalogue counts by state, from the database rather than from a page.</summary>
    Task<CatalogueSummaryDto> GetCatalogueSummaryAsync(CancellationToken cancellationToken);

    /// <summary>Screen 138 — customer-base totals, for the same reason.</summary>
    Task<CustomerSummaryDto> GetCustomerSummaryAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CampaignPerformanceDto>> GetCampaignPerformanceAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    Task<FinancialTotalsDto> GetFinancialTotalsAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    // --- itemised report rows ---------------------------------------------
    //
    // What the exports actually carry. The aggregates above answer "how much"
    // for a chart; these answer "which one, to whom, when, for how much" for
    // somebody reconciling the shop's month.

    Task<IReadOnlyList<SalesDetailRow>> GetSalesDetailAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrdersDetailRow>> GetOrdersDetailAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomersDetailRow>> GetCustomersDetailAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<InventoryDetailRow>> GetInventoryDetailAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<FinancialDetailRow>> GetFinancialDetailAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<CampaignsDetailRow>> GetCampaignsDetailAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);
}

/// <summary>
/// The panel's writes.
/// </summary>
/// <remarks>
/// One port for all of them because they share a guarantee rather than a
/// subject: each is role-gated, each is audited, and each is a single
/// <c>SaveChanges</c>. Splitting by entity would spread that guarantee across
/// a dozen files.
/// </remarks>
public interface IAdminRepository
{
    /// <summary>A wallet top-up awaiting an operator's decision.</summary>
    Task<WalletTopUp?> FindWalletTopUpAsync(Guid id, CancellationToken cancellationToken);

    Task<Product?> FindProductAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The product with its row locked, for a write that reads the stock count
    /// and then changes it.
    /// </summary>
    /// <remarks>
    /// The same lock the checkout takes before it sells a unit, and for the same
    /// reason: recording a movement is a read-modify-write on <c>Stock</c>, and
    /// EF writes the result as an absolute value. Unlocked, two receipts of ten
    /// units both read the old count and both write old + 10 — twenty units
    /// arrive and ten are recorded. A stocktake landing while an order is being
    /// placed is the same race with the shop's own sales on the other side of
    /// it. Must be called inside a transaction, or the lock is released before
    /// the write it is guarding.
    /// </remarks>
    Task<Product?> FindProductForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<Product?> FindProductWithDetailAsync(Guid id, CancellationToken cancellationToken);

    void AddProduct(Product product);

    Task<bool> ProductSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>
    /// Every slug already taken in one family: <paramref name="stem"/> itself
    /// and anything beginning with it.
    /// </summary>
    /// <remarks>
    /// Deliberately one query rather than a probe per candidate. Finding a free
    /// suffix used to ask the database "is <c>x-2</c> taken? is <c>x-3</c>
    /// taken?" up to 998 times for one save, which is 998 round trips on the
    /// exact case — a popular product title — where it is slowest, and where
    /// the answer to all of them is already in one index range.
    /// </remarks>
    Task<IReadOnlyList<string>> ProductSlugFamilyAsync(string stem, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>The product's variant axes with their options, for screen 107's replace-in-full save.</summary>
    Task<IReadOnlyList<ProductVariantAxis>> ListVariantAxesAsync(Guid productId, CancellationToken cancellationToken);

    void ReplaceVariantAxes(Guid productId, IReadOnlyList<ProductVariantAxis> existing, IEnumerable<ProductVariantAxis> replacement);

    Task<IReadOnlyList<ProductSku>> ListSkusAsync(Guid productId, CancellationToken cancellationToken);

    void ReplaceSkus(Guid productId, IReadOnlyList<ProductSku> existing, IEnumerable<ProductSku> replacement);

    /// <summary>Whether any *other* product already uses one of these codes — the uniqueness screen 108 enforces.</summary>
    Task<bool> SkuCodeTakenAsync(IReadOnlyList<string> codes, Guid exceptProductId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductAttribute>> ListAttributesAsync(Guid productId, CancellationToken cancellationToken);

    void ReplaceAttributes(Guid productId, IReadOnlyList<ProductAttribute> existing, IEnumerable<ProductAttribute> replacement);

    Task<IReadOnlyList<Domain.Catalogue.ProductVolumeTier>> ListVolumeTiersAsync(
        Guid productId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Swaps a product's whole volume ladder.
    /// </summary>
    /// <remarks>
    /// Replaced rather than merged, like the variants and attributes beside it:
    /// the screen posts the ladder it is showing, and a merge would leave a rung
    /// the operator had deleted still pricing quotes.
    /// </remarks>
    void ReplaceVolumeTiers(
        Guid productId,
        IReadOnlyList<Domain.Catalogue.ProductVolumeTier> existing,
        IEnumerable<Domain.Catalogue.ProductVolumeTier> replacement);

    /// <summary>
    /// Every catalogue row the given slugs and ids name, in one read.
    /// </summary>
    /// <remarks>
    /// The panel posts a *set* — the categories a product is filed under, the
    /// collections it belongs to, the products a collection holds — and
    /// resolving those one reference at a time is one round trip per element.
    /// A collection of two hundred products cost two hundred queries to save.
    /// Which reference matched what is worked out by the caller from the rows
    /// these return, so a reference that names nothing simply has no row.
    /// </remarks>
    Task<IReadOnlyList<Category>> FindCategoriesAsync(
        IReadOnlyCollection<string> slugs,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Collection>> FindCollectionsAsync(
        IReadOnlyCollection<string> slugs,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> FindProductsAsync(
        IReadOnlyCollection<string> slugs,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    Task<Category?> FindCategoryAsync(Guid id, CancellationToken cancellationToken);

    Task<Category?> FindCategoryBySlugAsync(string slug, CancellationToken cancellationToken);

    void AddCategory(Category category);

    Task<Brand?> FindBrandAsync(Guid id, CancellationToken cancellationToken);

    Task<Brand?> FindBrandBySlugAsync(string slug, CancellationToken cancellationToken);

    void AddBrand(Brand brand);

    Task<Collection?> FindCollectionAsync(Guid id, CancellationToken cancellationToken);

    void AddCollection(Collection collection);

    /// <summary>
    /// Every collection membership row for the given collections.
    /// </summary>
    /// <remarks>
    /// The product form needs two things from these: which of them name the
    /// product being saved, and — for the ones that do not yet — what the last
    /// position in each collection is, so a product joining one lands at the
    /// end rather than fighting for position zero with whatever is already
    /// there. Both come out of the same read.
    /// </remarks>
    Task<IReadOnlyList<CollectionProduct>> ListCollectionMembershipsAsync(
        IReadOnlyCollection<Guid> collectionIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CollectionProduct>> ListProductMembershipsAsync(
        Guid productId,
        CancellationToken cancellationToken);

    void ReplaceProductMemberships(
        IReadOnlyList<CollectionProduct> removed,
        IEnumerable<CollectionProduct> added);

    Task<ContentEntry?> FindContentAsync(Guid id, CancellationToken cancellationToken);

    void AddContent(ContentEntry entry);

    Task<Campaign?> FindCampaignAsync(Guid id, CancellationToken cancellationToken);

    void AddCampaign(Campaign campaign);

    Task<Coupon?> FindCouponAsync(Guid id, CancellationToken cancellationToken);

    Task<Coupon?> FindCouponByCodeAsync(string code, CancellationToken cancellationToken);

    void AddCoupon(Coupon coupon);

    void AddStockMovement(Domain.Inventory.StockMovement movement);

    Task<Order?> FindOrderAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The order with its lines and timeline, its row locked, for a write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cancelling reads the status, decides a refund from it and then writes
    /// the status — a read-modify-write, and the status is the only thing
    /// stopping the refund being paid twice. Unlocked, a double-clicked cancel
    /// puts the money back twice and the stock back twice. Same reasoning, and
    /// the same fix, as the wallet top-up decision. Must be called inside a
    /// transaction; a <c>FOR UPDATE</c> in autocommit is released too early to
    /// mean anything.
    /// </para>
    /// <para>
    /// The status control uses it for the same reason, which is why this is no
    /// longer named after cancelling. It reads the status, decides from it
    /// whether the move is legal, and then writes it — and unlocked, two
    /// operators moving one order at the same moment both passed that check,
    /// both appended a timeline event and both notified the customer, leaving
    /// an order whose status disagreed with its own history.
    /// </para>
    /// </remarks>
    Task<Order?> FindOrderForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The customer with their row locked, for a refund that reads the balance and then changes it.</summary>
    Task<Customer?> FindCustomerForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The order's products, locked, so restocking does not race a stocktake or a concurrent order.</summary>
    Task<IReadOnlyList<Product>> LoadProductsForUpdateAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);

    /// <summary>The order's variants, locked, for the lines that sold one.</summary>
    Task<IReadOnlyList<ProductSku>> LoadSkusForUpdateAsync(
        IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken);

    void AddWalletTransaction(WalletTransaction transaction);

    /// <summary>
    /// Tracks a timeline entry an already-loaded order just produced.
    /// </summary>
    /// <remarks>
    /// Adding it to the order's own collection is not enough: EF decides the
    /// state of an entity discovered through a tracked parent from whether its
    /// key is set, and every entity here assigns its own GUID at construction.
    /// Without this the new row would be saved as an UPDATE of a row that does
    /// not exist. The same applies to <see cref="AddBusinessRequestEvent"/> and
    /// to support messages.
    /// </remarks>
    void AddOrderTimelineEvent(OrderTimelineEvent entry);

    /// <summary>
    /// The return request with its items, its row locked, for a decision.
    /// </summary>
    /// <remarks>
    /// Locked for the reason <see cref="FindOrderForUpdateAsync"/> is: deciding
    /// one reads its status, computes a refund from it and then writes the
    /// status, and that status is the only thing stopping a double-clicked
    /// approve paying the refund twice. Must be called inside a transaction, or
    /// the lock is released before the write it is guarding.
    /// </remarks>
    Task<ReturnRequest?> FindReturnRequestForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tracks a return timeline entry, for the reason <see cref="AddOrderTimelineEvent"/> exists.</summary>
    void AddReturnTimelineEvent(ReturnTimelineEvent entry);

    /// <summary>
    /// What this order's other returns have already paid back.
    /// </summary>
    /// <remarks>
    /// <paramref name="exceptReturnId"/> excludes the one being decided, whose
    /// refund is in the change tracker and not yet committed — counting it from
    /// both sides would double it. Used to tell a partial return from the one
    /// that finally completes the order.
    /// </remarks>
    Task<Money> SumRefundedReturnsAsync(Guid orderId, Guid exceptReturnId, CancellationToken cancellationToken);

    Task<BusinessRequest?> FindBusinessRequestAsync(Guid id, CancellationToken cancellationToken);

    void AddBusinessRequestEvent(BusinessRequestEvent entry);

    void AddNotificationCampaign(NotificationCampaign campaign);

    void AddReportExport(ReportExport export);

    /// <summary>Oldest first, up to <paramref name="limit"/> — the queue the background export worker drains.</summary>
    Task<IReadOnlyList<ReportExport>> ListQueuedReportExportsAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Puts jobs left <c>Running</c> by a dead process back on the queue.
    /// </summary>
    /// <remarks>
    /// A worker marks a row <c>Running</c> before it starts, and the queue only
    /// looks for <c>Queued</c> — so a restart, an OOM kill or a deploy in the
    /// middle of a job stranded that row permanently. The panel went on showing
    /// "in progress" for a job nothing was doing, and the only way out was to
    /// edit the database by hand.
    /// </remarks>
    Task<int> ReclaimStalledJobsAsync(DateTimeOffset startedBefore, CancellationToken cancellationToken);

    Task<ReportExport?> FindReportExportAsync(Guid id, CancellationToken cancellationToken);

    void AddBackupJob(BackupJob job);

    /// <summary>Newest first — what screen 156's table renders.</summary>
    Task<IReadOnlyList<BackupJob>> ListBackupJobsAsync(CancellationToken cancellationToken);

    Task<BackupJob?> FindBackupJobAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The oldest backup still waiting, or null.
    /// </summary>
    /// <remarks>
    /// One at a time by design — a dump is heavy on the database and on the
    /// disk, and two of them racing is how the backup becomes the outage.
    /// </remarks>
    Task<BackupJob?> FindNextQueuedBackupAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RolePermission>> ListRolePermissionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the whole non-owner grant set with <paramref name="grants"/>
    /// in one transaction — the matrix always saves its full state, never a
    /// single cell, so a partial write here could leave a role with neither
    /// its old grants nor its new ones.
    /// </summary>
    Task ReplaceRolePermissionsAsync(IReadOnlyList<RolePermission> grants, CancellationToken cancellationToken);

    /// <summary>One magazine article, blocks included, for editing.</summary>
    /// <remarks>
    /// The blocks come with it because a save replaces them wholesale — loading
    /// the article without them would delete the body of every article anybody
    /// edited.
    /// </remarks>
    Task<Article?> FindArticleAsync(Guid id, CancellationToken cancellationToken);

    void AddArticle(Article article);

    /// <summary>
    /// Whether another article already answers to this slug.
    /// </summary>
    /// <remarks>
    /// The slug is the article's address on the storefront, so two of them is
    /// one article nobody can reach. Checked before the index refuses it, so
    /// the panel gets a field error rather than a 500.
    /// </remarks>
    Task<bool> IsArticleSlugTakenAsync(string slug, Guid excluding, CancellationToken cancellationToken);

    Task<AdminUser?> FindAdminUserAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The grant a shop account already holds, if any — so one person cannot be appointed twice.</summary>
    Task<AdminUser?> FindAdminUserByCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    void AddAdminUser(AdminUser user);

    /// <summary>
    /// Grants one operator one section, and revokes one.
    /// </summary>
    /// <remarks>
    /// Written through the set rather than through <see cref="AdminUser.Sections"/>
    /// alone, and that is not a style preference. Every entity here carries a
    /// <c>Guid</c> it gives itself at construction, so a row appended to a
    /// loaded collection arrives at the change tracker with its key already
    /// set — and a tracked child with a known key is taken for one that exists.
    /// The save came out as <c>UPDATE admin_user_sections … WHERE "Id" = …</c>
    /// against a row that had never been inserted, which affects nothing, which
    /// EF reports as a concurrency conflict. So every grant made from the edit
    /// sheet failed with «این مقدار تکراری است» while the create path, whose
    /// whole graph is added at once, worked.
    /// </remarks>
    void AddAdminUserSection(AdminUserSection section);

    /// <inheritdoc cref="AddAdminUserSection"/>
    void RemoveAdminUserSection(AdminUserSection section);

    /// <summary>
    /// Whether another operator already answers to <paramref name="email"/> or
    /// <paramref name="phone"/>.
    /// </summary>
    /// <remarks>
    /// Both are sign-in identities, so both have to resolve to exactly one
    /// operator — the database says so too, but a unique-index violation
    /// arrives as a 500 rather than as the field error the form can point at.
    /// The email comparison is case-insensitive because sign-in's is: without
    /// that, <c>Sara@bojan.com</c> and <c>sara@bojan.com</c> are two rows the
    /// index accepts and one identity the login resolves arbitrarily.
    /// </remarks>
    /// <param name="excluding">
    /// The operator being edited, whose own address is not a clash with itself.
    /// </param>
    Task<bool> IsAdminIdentityTakenAsync(
        string email,
        string? phone,
        Guid? excluding,
        CancellationToken cancellationToken);

    /// <summary>
    /// How many operators can still reach the owner-only screens.
    /// </summary>
    /// <remarks>
    /// Counted before demoting or suspending one, because reaching zero is the
    /// state nothing in the panel can undo: settings, the permission grid and
    /// this very screen are owner-only, so the shop would be left with no way
    /// to appoint another and the only route back would be editing the database
    /// by hand.
    /// </remarks>
    Task<int> CountActiveOwnersAsync(CancellationToken cancellationToken);

    Task<ApiKey?> FindApiKeyAsync(Guid id, CancellationToken cancellationToken);

    void AddApiKey(ApiKey key);

    Task<SettingEntry?> FindSettingAsync(string section, string key, CancellationToken cancellationToken);

    void AddSetting(SettingEntry entry);

    Task<Customer?> FindCustomerAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The sign-in number has to reach exactly one customer.</summary>
    Task<bool> IsCustomerPhoneTakenAsync(string phone, Guid excluding, CancellationToken cancellationToken);

    /// <summary>So does the shop's own reference — it is quoted back off an order.</summary>
    Task<bool> IsCustomerCodeTakenAsync(string code, Guid excluding, CancellationToken cancellationToken);

    /// <summary>Email is the other sign-in identity, and optional, so only a set one is checked.</summary>
    Task<bool> IsCustomerEmailTakenAsync(string email, Guid excluding, CancellationToken cancellationToken);

    /// <summary>
    /// Whether this customer has traded — an order, a wallet movement, a support
    /// ticket.
    /// </summary>
    /// <remarks>
    /// The three relationships that restrict a delete. Asked before the delete
    /// so the refusal is a sentence about orders rather than a foreign-key
    /// violation arriving as a conflict with no explanation in it.
    /// </remarks>
    Task<bool> CustomerHasTradingHistoryAsync(Guid id, CancellationToken cancellationToken);

    void RemoveCustomer(Customer customer);

    void AddCustomerNotification(CustomerNotification notification);

    /// <summary>
    /// Removes a message the shop sent — a broadcast, or one customer's copy.
    /// </summary>
    /// <remarks>
    /// Deleting a broadcast takes the rows it fanned out with it: the campaign
    /// is the only record of why a thousand customers have the same
    /// notification, and leaving those behind would orphan them against a
    /// campaign id that no longer resolves.
    /// </remarks>
    Task<bool> RemoveNotificationAsync(string kind, Guid id, CancellationToken cancellationToken);

}
