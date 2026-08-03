using Bojan.Application.Administration;
using Bojan.Application.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Phase 6 — the panel's reads, all under <c>/api/admin</c>.
/// </summary>
/// <remarks>
/// <para>
/// A different base URL and a different credential from the storefront's: the
/// panel's <c>.env.example</c> points at <c>/api/admin</c>, and
/// <c>BACKEND.md</c> is explicit that <c>POST /api/admin/products</c> is a
/// different endpoint from <c>GET /api/products</c>.
/// </para>
/// <para>
/// Role gates match the panel's own per-resource declaration
/// (<c>apps/admin/src/lib/api/resources.ts</c>): reads use the same gate as the
/// write they feed, so an operator cannot read a list they could not act on.
/// </para>
/// <para>
/// The panel has no read layer yet — it renders fixtures directly — so nothing
/// calls these until the mirroring frontend task lands. They are built to the
/// query-string keys those screens already use (<c>q</c>, <c>status</c>,
/// <c>page</c>).
/// </para>
/// </remarks>
public static class AdminReadEndpoints
{
    public static void MapAdminReadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(string.Empty).NoStore();

        group.MapGet("/orders", ListOrders).RequireAuthorization(AuthorizationPolicies.AdminOrders);
        group.MapGet("/orders/{id:guid}", GetOrder).RequireAuthorization(AuthorizationPolicies.AdminOrders);

        group.MapGet("/products", ListProducts).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/products/{id:guid}", GetProduct).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/products/{id:guid}/variants", GetProductVariants).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/products/{id:guid}/skus", GetProductSkus).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/products/{id:guid}/attributes", GetProductAttributes).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);

        // Paged and filtered for the categories/brands list screens, and
        // large-paged by default so the product/category/brand form pickers
        // (which call these with no query at all) still get the whole list.
        // AdminCategoryDto/AdminBrandDto are a structural superset of the
        // picker's CatalogueOptionDto shape ({ slug, name }) — see those
        // DTOs' remarks in AdminContracts.cs.
        group.MapGet("/categories", ListCategories).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/categories/{id:guid}", GetCategory).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/brands", ListBrands).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/brands/{id:guid}", GetBrand).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/collections", ListCollections).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/collections/{id:guid}", GetCollection).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);

        group.MapGet("/customers", ListCustomers).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("/customers/{id:guid}", GetCustomer).RequireAuthorization(AuthorizationPolicies.Admin);

        group.MapGet("/inventory", ListInventory).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/inventory/movements", ListStockMovements).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);

        group.MapGet("/business-requests", ListBusinessRequests).RequireAuthorization(AuthorizationPolicies.AdminSales);
        group.MapGet("/coupons", ListCoupons).RequireAuthorization(AuthorizationPolicies.AdminSales);
        group.MapGet("/coupons/{id:guid}", GetCoupon).RequireAuthorization(AuthorizationPolicies.AdminSales);
        group.MapGet("/campaigns", ListCampaigns).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/campaigns/{id:guid}", GetCampaign).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/content", ListContent).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapGet("/content/{id:guid}", GetContent).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);

        group.MapGet("/support/threads", ListSupportThreads).RequireAuthorization(AuthorizationPolicies.AdminSupport);
        group.MapGet("/support/threads/{id:guid}", GetSupportThread).RequireAuthorization(AuthorizationPolicies.AdminSupport);
        group.MapGet("/support/canned-replies", ListCannedReplies).RequireAuthorization(AuthorizationPolicies.AdminSupport);

        group.MapGet("/backups", ListBackups).RequireAuthorization(AuthorizationPolicies.AdminOwner);
        group.MapGet("/roles/permissions", ListRolePermissions).RequireAuthorization(AuthorizationPolicies.AdminOwner);
        group.MapGet("/backups/{id:guid}/download", DownloadBackup).RequireAuthorization(AuthorizationPolicies.AdminOwner);
        // Owner only, matching the decision endpoint: the queue and the power
        // to settle it belong to the same person.
        group.MapGet("/wallet/topups", ListWalletTopUps).RequireAuthorization(AuthorizationPolicies.AdminOwner);
        group.MapGet("/settings/audit", ListAudit).RequireAuthorization(AuthorizationPolicies.AdminOwner);
        group.MapGet("/settings/users", ListAdminUsers).RequireAuthorization(AuthorizationPolicies.AdminOwner);
        group.MapGet("/settings/api-keys", ListApiKeys).RequireAuthorization(AuthorizationPolicies.AdminOwner);
        group.MapGet("/settings/{section}", GetSettings).RequireAuthorization(AuthorizationPolicies.AdminOwner);

        // Screen 157. Separate from the unauthenticated /health probe, which
        // answers only "up" — per-dependency detail names the pieces of the
        // deployment and is not something to publish.
        group.MapGet("/system/health", GetSystemHealth).RequireAuthorization(AuthorizationPolicies.AdminOwner);

        // Dashboard and reports — screens 92 and 133-140.
        group.MapGet("/dashboard", GetDashboard).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("/reports/sales", GetSales).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("/reports/order-status", GetOrderStatusCounts).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("/reports/top-products", GetTopProducts).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("/reports/customer-growth", GetCustomerGrowth).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("/reports/stock-levels", GetStockLevels).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("/reports/catalogue-summary", GetCatalogueSummary).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("/reports/customer-summary", GetCustomerSummary).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("/reports/campaigns", GetCampaignPerformance).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("/reports/financial", GetFinancialTotals).RequireAuthorization(AuthorizationPolicies.AdminOwner);
    }

    /// <summary>
    /// The shared filter, bound from the query keys the panel's screens already
    /// put in the URL.
    /// </summary>
    private static AdminListQuery ListQuery(
        string? q, string? status, string? kind, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int page, int pageSize) =>
        new(q, status, kind, fromUtc, toUtc, page, pageSize);

    private static async Task<IResult> ListOrders(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListOrdersAsync(ListQuery(q, status, null, from, to, page, pageSize), cancellationToken));

    private static async Task<IResult> GetOrder(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetOrderAsync(id, cancellationToken) is { } order ? Results.Ok(order) : ApiResults.NotFound();

    private static async Task<IResult> ListProducts(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListProductsAsync(ListQuery(q, status, null, null, null, page, pageSize), cancellationToken));

    private static async Task<IResult> GetProduct(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetProductAsync(id, cancellationToken) is { } product ? Results.Ok(product) : ApiResults.NotFound();

    // Screens 106-108. Each returns a list rather than 404 for a product with
    // none: an empty table is the correct answer, and the screens are reached
    // from the product itself, which the route already proved exists.
    private static async Task<IResult> GetProductVariants(
        Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.GetProductVariantsAsync(id, cancellationToken));

    private static async Task<IResult> GetProductSkus(
        Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.GetProductSkusAsync(id, cancellationToken));

    private static async Task<IResult> GetProductAttributes(
        Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.GetProductAttributesAsync(id, cancellationToken));

    private static async Task<IResult> ListCategories(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int? pageSize = null) =>
        Results.Ok(await queries.ListCategoriesAsync(
            ListQuery(q, status, null, null, null, page, pageSize ?? AdminListQuery.MaxPageSize), cancellationToken));

    private static async Task<IResult> GetCategory(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetCategoryAsync(id, cancellationToken) is { } category ? Results.Ok(category) : ApiResults.NotFound();

    private static async Task<IResult> ListBrands(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int? pageSize = null) =>
        Results.Ok(await queries.ListBrandsAsync(
            ListQuery(q, status, null, null, null, page, pageSize ?? AdminListQuery.MaxPageSize), cancellationToken));

    private static async Task<IResult> GetBrand(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetBrandAsync(id, cancellationToken) is { } brand ? Results.Ok(brand) : ApiResults.NotFound();

    private static async Task<IResult> ListCollections(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListCollectionsAsync(ListQuery(q, status, null, null, null, page, pageSize), cancellationToken));

    private static async Task<IResult> GetCollection(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetCollectionAsync(id, cancellationToken) is { } collection ? Results.Ok(collection) : ApiResults.NotFound();

    private static async Task<IResult> ListCustomers(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListCustomersAsync(ListQuery(q, status, null, null, null, page, pageSize), cancellationToken));

    private static async Task<IResult> GetCustomer(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetCustomerAsync(id, cancellationToken) is { } customer ? Results.Ok(customer) : ApiResults.NotFound();

    private static async Task<IResult> ListInventory(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListInventoryAsync(ListQuery(q, status, null, null, null, page, pageSize), cancellationToken));

    private static async Task<IResult> ListStockMovements(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? kind = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListStockMovementsAsync(
            ListQuery(null, null, kind, from, to, page, pageSize), cancellationToken));

    private static async Task<IResult> ListBusinessRequests(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListBusinessRequestsAsync(
            ListQuery(q, status, null, null, null, page, pageSize), cancellationToken));

    private static async Task<IResult> ListCoupons(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListCouponsAsync(ListQuery(q, status, null, null, null, page, pageSize), cancellationToken));

    private static async Task<IResult> GetCoupon(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetCouponAsync(id, cancellationToken) is { } coupon ? Results.Ok(coupon) : ApiResults.NotFound();

    private static async Task<IResult> ListCampaigns(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListCampaignsAsync(ListQuery(q, status, null, null, null, page, pageSize), cancellationToken));

    private static async Task<IResult> GetCampaign(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetCampaignAsync(id, cancellationToken) is { } campaign ? Results.Ok(campaign) : ApiResults.NotFound();

    private static async Task<IResult> ListContent(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] string? kind = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListContentAsync(ListQuery(q, status, kind, null, null, page, pageSize), cancellationToken));

    private static async Task<IResult> GetContent(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetContentAsync(id, cancellationToken) is { } content ? Results.Ok(content) : ApiResults.NotFound();

    private static async Task<IResult> ListSupportThreads(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListSupportThreadsAsync(
            ListQuery(q, status, null, null, null, page, pageSize), cancellationToken));

    private static async Task<IResult> GetSupportThread(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetSupportThreadAsync(id, cancellationToken) is { } thread ? Results.Ok(thread) : ApiResults.NotFound();

    private static async Task<IResult> ListCannedReplies(IAdminQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListCannedRepliesAsync(cancellationToken));

    private static async Task<IResult> ListBackups(
        AdminOperationsService operations, CancellationToken cancellationToken) =>
        Results.Ok(await operations.ListBackupJobsAsync(cancellationToken));

    private static async Task<IResult> DownloadBackup(
        Guid id, AdminOperationsService operations, CancellationToken cancellationToken)
    {
        var file = await operations.GetBackupFileAsync(id, cancellationToken);
        return file is { } found
            ? Results.File(found.Content, "application/json", found.FileName)
            : ApiResults.NotFound();
    }

    private static async Task<IResult> ListRolePermissions(
        AdminOperationsService operations, CancellationToken cancellationToken) =>
        Results.Ok(await operations.ListRolePermissionsAsync(cancellationToken));

    private static async Task<IResult> ListAudit(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListAuditAsync(ListQuery(q, null, null, from, to, page, pageSize), cancellationToken));

    private static async Task<IResult> ListWalletTopUps(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListWalletTopUpsAsync(
            ListQuery(q, null, null, null, null, page, pageSize), status, cancellationToken));

    private static async Task<IResult> ListAdminUsers(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListAdminUsersAsync(ListQuery(q, null, null, null, null, page, pageSize), cancellationToken));

    private static async Task<IResult> ListApiKeys(IAdminQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListApiKeysAsync(cancellationToken));

    private static async Task<IResult> GetSettings(string section, IAdminQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.GetSettingsAsync(section, cancellationToken));

    private static async Task<IResult> GetDashboard(IAdminQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.GetDashboardKpisAsync(cancellationToken));

    /// <summary>
    /// Screen 157 — runs the registered health checks and reports each one.
    /// </summary>
    /// <remarks>
    /// The checks are run on request rather than read from a store, so the
    /// latency and the timestamp are this call's own. The panel used to show
    /// four invented services with invented latencies; what it shows now is
    /// exactly what is registered in <c>Program.cs</c>, which today is the
    /// database. A dependency joins this list by being registered as a check,
    /// not by being added to a fixture.
    /// </remarks>
    private static async Task<IResult> GetSystemHealth(
        HealthCheckService health, CancellationToken cancellationToken)
    {
        var report = await health.CheckHealthAsync(cancellationToken);

        var services = report.Entries.Select(entry => new ServiceHealthDto(
            entry.Key,
            entry.Key,
            entry.Value.Status switch
            {
                HealthStatus.Healthy => "operational",
                HealthStatus.Degraded => "degraded",
                _ => "down",
            },
            (int)entry.Value.Duration.TotalMilliseconds,
            DateTimeOffset.UtcNow,
            // The exception message can name a host or a credential, so only
            // the check's own description travels — never the raw failure.
            entry.Value.Status == HealthStatus.Healthy ? null : entry.Value.Description));

        return Results.Ok(services.OrderBy(service => service.Name).ToList());
    }

    /// <summary>
    /// Defaults to the last 30 days when no range is given, so a report screen
    /// that has not yet had its picker touched still draws something.
    /// </summary>
    private static (DateTimeOffset From, DateTimeOffset To) Range(DateTimeOffset? from, DateTimeOffset? to)
    {
        var end = to ?? DateTimeOffset.UtcNow;
        return (from ?? end.AddDays(-30), end);
    }

    private static async Task<IResult> GetSales(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? grouping = null)
    {
        var (start, end) = Range(from, to);
        var period = Enum.TryParse<ReportGrouping>(grouping, ignoreCase: true, out var parsed) ? parsed : ReportGrouping.Day;
        return Results.Ok(await queries.GetSalesAsync(start, end, period, cancellationToken));
    }

    private static async Task<IResult> GetOrderStatusCounts(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        var (start, end) = Range(from, to);
        return Results.Ok(await queries.GetOrderStatusCountsAsync(start, end, cancellationToken));
    }

    private static async Task<IResult> GetTopProducts(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int limit = 10)
    {
        var (start, end) = Range(from, to);
        return Results.Ok(await queries.GetTopProductsAsync(start, end, limit, cancellationToken));
    }

    private static async Task<IResult> GetCustomerGrowth(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? grouping = null)
    {
        var (start, end) = Range(from, to);
        var period = Enum.TryParse<ReportGrouping>(grouping, ignoreCase: true, out var parsed) ? parsed : ReportGrouping.Month;
        return Results.Ok(await queries.GetCustomerGrowthAsync(start, end, period, cancellationToken));
    }

    private static async Task<IResult> GetStockLevels(IAdminQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.GetStockLevelsAsync(cancellationToken));

    private static async Task<IResult> GetCatalogueSummary(IAdminQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.GetCatalogueSummaryAsync(cancellationToken));

    private static async Task<IResult> GetCustomerSummary(IAdminQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.GetCustomerSummaryAsync(cancellationToken));

    private static async Task<IResult> GetCampaignPerformance(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        var (start, end) = Range(from, to);
        return Results.Ok(await queries.GetCampaignPerformanceAsync(start, end, cancellationToken));
    }

    private static async Task<IResult> GetFinancialTotals(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        var (start, end) = Range(from, to);
        return Results.Ok(await queries.GetFinancialTotalsAsync(start, end, cancellationToken));
    }
}
