using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Admin;
using Bojan.Domain.Business;
using Bojan.Domain.Catalogue;
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

    public AdminListQuery Normalised() => this with
    {
        Page = Page < 1 ? 1 : Page,
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

    Task<Paged<AdminProductDto>> ListProductsAsync(AdminListQuery query, CancellationToken cancellationToken);

    Task<AdminProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Screen 107 — the product's axes and their options.</summary>
    Task<IReadOnlyList<AdminVariantAxisDto>> GetProductVariantsAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Screen 108 — the product's sellable combinations.</summary>
    Task<IReadOnlyList<AdminSkuDto>> GetProductSkusAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Screen 106 — the product's attributes.</summary>
    Task<IReadOnlyList<AdminAttributeDto>> GetProductAttributesAsync(Guid productId, CancellationToken cancellationToken);

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

    Task<Paged<AdminUserDto>> ListAdminUsersAsync(AdminListQuery query, CancellationToken cancellationToken);

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

    Task<Product?> FindProductWithDetailAsync(Guid id, CancellationToken cancellationToken);

    void AddProduct(Product product);

    Task<bool> ProductSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>The product's variant axes with their options, for screen 107's replace-in-full save.</summary>
    Task<IReadOnlyList<ProductVariantAxis>> ListVariantAxesAsync(Guid productId, CancellationToken cancellationToken);

    void ReplaceVariantAxes(Guid productId, IReadOnlyList<ProductVariantAxis> existing, IEnumerable<ProductVariantAxis> replacement);

    Task<IReadOnlyList<ProductSku>> ListSkusAsync(Guid productId, CancellationToken cancellationToken);

    void ReplaceSkus(Guid productId, IReadOnlyList<ProductSku> existing, IEnumerable<ProductSku> replacement);

    /// <summary>Whether any *other* product already uses one of these codes — the uniqueness screen 108 enforces.</summary>
    Task<bool> SkuCodeTakenAsync(IReadOnlyList<string> codes, Guid exceptProductId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductAttribute>> ListAttributesAsync(Guid productId, CancellationToken cancellationToken);

    void ReplaceAttributes(Guid productId, IReadOnlyList<ProductAttribute> existing, IEnumerable<ProductAttribute> replacement);

    Task<Category?> FindCategoryAsync(Guid id, CancellationToken cancellationToken);

    Task<Category?> FindCategoryBySlugAsync(string slug, CancellationToken cancellationToken);

    void AddCategory(Category category);

    Task<Brand?> FindBrandAsync(Guid id, CancellationToken cancellationToken);

    Task<Brand?> FindBrandBySlugAsync(string slug, CancellationToken cancellationToken);

    void AddBrand(Brand brand);

    Task<Collection?> FindCollectionAsync(Guid id, CancellationToken cancellationToken);

    void AddCollection(Collection collection);

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

    Task<BusinessRequest?> FindBusinessRequestAsync(Guid id, CancellationToken cancellationToken);

    void AddBusinessRequestEvent(BusinessRequestEvent entry);

    void AddNotificationCampaign(NotificationCampaign campaign);

    void AddReportExport(ReportExport export);

    void AddBackupJob(BackupJob job);

    /// <summary>Newest first — what screen 156's table renders.</summary>
    Task<IReadOnlyList<BackupJob>> ListBackupJobsAsync(CancellationToken cancellationToken);

    Task<BackupJob?> FindBackupJobAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<RolePermission>> ListRolePermissionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the whole non-owner grant set with <paramref name="grants"/>
    /// in one transaction — the matrix always saves its full state, never a
    /// single cell, so a partial write here could leave a role with neither
    /// its old grants nor its new ones.
    /// </summary>
    Task ReplaceRolePermissionsAsync(IReadOnlyList<RolePermission> grants, CancellationToken cancellationToken);

    Task<AdminUser?> FindAdminUserAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiKey?> FindApiKeyAsync(Guid id, CancellationToken cancellationToken);

    void AddApiKey(ApiKey key);

    Task<SettingEntry?> FindSettingAsync(string section, string key, CancellationToken cancellationToken);

    void AddSetting(SettingEntry entry);

    Task<Customer?> FindCustomerAsync(Guid id, CancellationToken cancellationToken);

    void AddCustomerNotification(CustomerNotification notification);

    /// <summary>Every customer id in an audience, for fanning a broadcast out into per-customer rows.</summary>
    Task<IReadOnlyList<Guid>> ListCustomerIdsAsync(string audience, CancellationToken cancellationToken);
}
