using System.Diagnostics;
using System.Runtime.InteropServices;
using Bojan.Api.Auth;
using Bojan.Application.Administration;
using Bojan.Application.Common;
using Bojan.Domain.Admin;
using Bojan.Application.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

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

        group.MapGet("/orders", ListOrders).RequireAuthorization(AuthorizationPolicies.AdminOrders).RequireSection(PanelSection.Orders);
        group.MapGet("/orders/{id:guid}", GetOrder).RequireAuthorization(AuthorizationPolicies.AdminOrders).RequireSection(PanelSection.Orders);

        // Invoices sit under the orders section rather than getting one of
        // their own: an invoice is a view of an order, and an operator trusted
        // to open an order already sees everything on it. A separate section
        // would be a permission that looks like it withholds something and
        // withholds nothing.
        group.MapGet("/invoices", ListInvoices).RequireAuthorization(AuthorizationPolicies.AdminOrders).RequireSection(PanelSection.Orders);
        group.MapGet("/orders/{id:guid}/invoice", GetInvoice).RequireAuthorization(AuthorizationPolicies.AdminOrders).RequireSection(PanelSection.Orders);

        // The returns queue sits with orders — same gate, same section — because
        // it is the same people doing it, and deciding one is a write on an
        // order's money. See the decision endpoint for why that gate is the
        // cancellation's rather than the settlement's.
        group.MapGet("/returns", ListReturns).RequireAuthorization(AuthorizationPolicies.AdminOrders).RequireSection(PanelSection.Orders);
        group.MapGet("/returns/{id:guid}", GetReturn).RequireAuthorization(AuthorizationPolicies.AdminOrders).RequireSection(PanelSection.Orders);

        group.MapGet("/products", ListProducts).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapGet("/products/{id:guid}", GetProduct).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapGet("/products/{id:guid}/variants", GetProductVariants).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapGet("/products/{id:guid}/skus", GetProductSkus).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapGet("/products/{id:guid}/attributes", GetProductAttributes).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);

        // Paged and filtered for the categories/brands list screens, and
        // large-paged by default so the product/category/brand form pickers
        // (which call these with no query at all) still get the whole list.
        // AdminCategoryDto/AdminBrandDto are a structural superset of the
        // picker's CatalogueOptionDto shape ({ slug, name }) — see those
        // DTOs' remarks in AdminContracts.cs.
        group.MapGet("/categories", ListCategories).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapGet("/categories/{id:guid}", GetCategory).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapGet("/brands", ListBrands).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapGet("/brands/{id:guid}", GetBrand).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapGet("/collections", ListCollections).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapGet("/collections/{id:guid}", GetCollection).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);

        // Names, phone numbers, email addresses and order history — the largest
        // pile of personal data the panel holds. It was open to every role,
        // which meant the catalogue operator, whose whole job is products, could
        // page through the customer base. The permission grid narrows this
        // further, but only once an owner has opened screen 146; the role gate
        // is what holds on an installation that never has.
        group.MapGet("/customers", ListCustomers).RequireAuthorization(AuthorizationPolicies.AdminOrders).RequireSection(PanelSection.Customers);
        group.MapGet("/customers/{id:guid}", GetCustomer).RequireAuthorization(AuthorizationPolicies.AdminOrders).RequireSection(PanelSection.Customers);

        group.MapGet("/inventory", ListInventory).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Inventory);
        group.MapGet("/inventory/movements", ListStockMovements).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Inventory);

        group.MapGet("/business-requests", ListBusinessRequests).RequireAuthorization(AuthorizationPolicies.AdminSales).RequireSection(PanelSection.Business);
        group.MapGet("/coupons", ListCoupons).RequireAuthorization(AuthorizationPolicies.AdminSales).RequireSection(PanelSection.Campaigns);
        group.MapGet("/coupons/{id:guid}", GetCoupon).RequireAuthorization(AuthorizationPolicies.AdminSales).RequireSection(PanelSection.Campaigns);
        group.MapGet("/campaigns", ListCampaigns).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Campaigns);
        group.MapGet("/campaigns/{id:guid}", GetCampaign).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Campaigns);
        group.MapGet("/content", ListContent).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Content);
        group.MapGet("/content/{id:guid}", GetContent).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Content);

        group.MapGet("/support/threads", ListSupportThreads).RequireAuthorization(AuthorizationPolicies.AdminSupport).RequireSection(PanelSection.Support);
        group.MapGet("/support/threads/{id:guid}", GetSupportThread).RequireAuthorization(AuthorizationPolicies.AdminSupport).RequireSection(PanelSection.Support);
        group.MapGet("/support/canned-replies", ListCannedReplies).RequireAuthorization(AuthorizationPolicies.AdminSupport).RequireSection(PanelSection.Support);

        // Live chat sits beside the ticket queue — same gate, same section —
        // but is its own resource: a ticket is a subject-and-status thread, a
        // chat conversation is just "this visitor" with no state of its own.
        group.MapGet("/chat/conversations", ListChatConversations).RequireAuthorization(AuthorizationPolicies.AdminSupport).RequireSection(PanelSection.Support);
        group.MapGet("/chat/conversations/{visitorId:guid}", GetChatConversation).RequireAuthorization(AuthorizationPolicies.AdminSupport).RequireSection(PanelSection.Support);

        group.MapGet("/backups", ListBackups).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);
        group.MapGet("/roles/permissions", ListRolePermissions).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);
        group.MapGet("/backups/{id:guid}/download", DownloadBackup).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);
        // Owner only, matching the decision endpoint: the queue and the power
        // to settle it belong to the same person.
        group.MapGet("/wallet/topups", ListWalletTopUps).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Customers);
        group.MapGet("/settings/audit", ListAudit).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);
        group.MapGet("/settings/users", ListAdminUsers).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);
        group.MapGet("/settings/api-keys", ListApiKeys).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);
        group.MapGet("/settings/{section}", GetSettings).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);

        // Screen 157. Separate from the unauthenticated /health probe, which
        // answers only "up" — per-dependency detail names the pieces of the
        // deployment and is not something to publish.
        group.MapGet("/system/health", GetSystemHealth).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);
        group.MapGet("/system/status", GetSystemStatus).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);

        // Dashboard and reports — screens 92 and 133-140.
        group.MapGet("/dashboard", GetDashboard).RequireAuthorization(AuthorizationPolicies.Admin).RequireSection(PanelSection.Reports);
        group.MapGet("/reports/sales", GetSales).RequireAuthorization(AuthorizationPolicies.Admin).RequireSection(PanelSection.Reports);
        group.MapGet("/reports/order-status", GetOrderStatusCounts).RequireAuthorization(AuthorizationPolicies.Admin).RequireSection(PanelSection.Reports);
        group.MapGet("/reports/top-products", GetTopProducts).RequireAuthorization(AuthorizationPolicies.Admin).RequireSection(PanelSection.Reports);
        group.MapGet("/reports/customer-growth", GetCustomerGrowth).RequireAuthorization(AuthorizationPolicies.Admin).RequireSection(PanelSection.Reports);
        group.MapGet("/reports/stock-levels", GetStockLevels).RequireAuthorization(AuthorizationPolicies.Admin).RequireSection(PanelSection.Reports);
        group.MapGet("/reports/catalogue-summary", GetCatalogueSummary).RequireAuthorization(AuthorizationPolicies.Admin).RequireSection(PanelSection.Reports);
        group.MapGet("/reports/customer-summary", GetCustomerSummary).RequireAuthorization(AuthorizationPolicies.Admin).RequireSection(PanelSection.Reports);
        group.MapGet("/reports/campaigns", GetCampaignPerformance).RequireAuthorization(AuthorizationPolicies.Admin).RequireSection(PanelSection.Reports);
        group.MapGet("/reports/financial", GetFinancialTotals).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Reports);
        group.MapGet("/reports/export/{id:guid}/download", DownloadReportExport).RequireAuthorization(AuthorizationPolicies.Admin).RequireSection(PanelSection.Reports);
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

    private static async Task<IResult> ListInvoices(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListInvoicesAsync(ListQuery(q, null, null, from, to, page, pageSize), cancellationToken));

    /// <summary>
    /// The invoice document for one order.
    /// </summary>
    /// <remarks>
    /// 404 rather than a 400 saying "not delivered yet" for an order that has
    /// no invoice. The resource genuinely does not exist — an undelivered order
    /// has no invoice to fetch — and the panel's list only ever links here for
    /// orders that do.
    /// </remarks>
    private static async Task<IResult> GetInvoice(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetInvoiceAsync(id, cancellationToken) is { } invoice ? Results.Ok(invoice) : ApiResults.NotFound();

    private static async Task<IResult> ListReturns(
        IAdminQueries queries,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminListQuery.DefaultPageSize) =>
        Results.Ok(await queries.ListReturnsAsync(ListQuery(q, status, null, from, to, page, pageSize), cancellationToken));

    private static async Task<IResult> GetReturn(Guid id, IAdminQueries queries, CancellationToken cancellationToken) =>
        await queries.GetReturnAsync(id, cancellationToken) is { } request ? Results.Ok(request) : ApiResults.NotFound();

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

    private static async Task<IResult> ListChatConversations(
        Bojan.Application.Support.LiveChatService chat, CancellationToken cancellationToken) =>
        Results.Ok(await chat.ListConversationsAsync(cancellationToken));

    /// <summary>Opening a conversation marks the visitor's messages read — see <c>LiveChatService.GetConversationAsSupportAsync</c>.</summary>
    private static async Task<IResult> GetChatConversation(
        Guid visitorId, Bojan.Application.Support.LiveChatService chat, CancellationToken cancellationToken) =>
        Results.Ok(await chat.GetConversationAsSupportAsync(visitorId, cancellationToken));

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

    private static async Task<IResult> DownloadReportExport(
        Guid id,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        // Scoped to whoever queued it — see GetReportExportFileAsync.
        var actor = user.AdminId ?? throw new InvalidOperationException(
            "An admin policy authorised a request with no operator id — the policy and CurrentUser disagree.");

        var file = await operations.GetReportExportFileAsync(id, actor, cancellationToken);
        return file is { } found
            ? Results.File(found.Content, "text/csv", found.FileName)
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
    /// The dashboard's server-status card — process uptime, memory, a sampled
    /// CPU load, host disk space, and the same database check <see cref="GetSystemHealth"/> runs.
    /// </summary>
    /// <remarks>
    /// CPU load has no cross-platform counter in .NET without an extra
    /// package, so it is measured directly: two samples of the process's own
    /// accumulated CPU time, a short wall-clock delay apart, divided by the
    /// wall-clock delay and the core count. That is what actually costs this
    /// endpoint ~200ms — a status card is read rarely enough that the delay is
    /// cheaper than carrying a background sampler for one number.
    /// </remarks>
    private static async Task<IResult> GetSystemStatus(
        HealthCheckService health, IHostEnvironment hostEnvironment, CancellationToken cancellationToken)
    {
        var process = Process.GetCurrentProcess();
        var cpuBefore = process.TotalProcessorTime;
        var clockBefore = Stopwatch.GetTimestamp();

        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);

        process.Refresh();
        var cpuAfter = process.TotalProcessorTime;
        var elapsed = Stopwatch.GetElapsedTime(clockBefore);

        double? cpuLoadPercent = elapsed.TotalMilliseconds > 0
            ? Math.Clamp(
                (cpuAfter - cpuBefore).TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100,
                0,
                100)
            : null;

        long? totalDisk = null;
        long? freeDisk = null;
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory) ?? "/");
            totalDisk = drive.TotalSize;
            freeDisk = drive.AvailableFreeSpace;
        }
        catch (IOException)
        {
            // Not every host exposes drive metrics for the app's mount (e.g. some
            // container filesystems) — the card shows the rest without it.
        }

        var report = await health.CheckHealthAsync(cancellationToken);
        var databaseHealthy = report.Entries.TryGetValue("database", out var dbEntry)
            ? dbEntry.Status == HealthStatus.Healthy
            : report.Status == HealthStatus.Healthy;

        return Results.Ok(new ServerStatusDto(
            Environment: hostEnvironment.EnvironmentName,
            DotnetVersion: Environment.Version.ToString(),
            OperatingSystem: RuntimeInformation.OSDescription,
            UptimeSeconds: (long)(DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds,
            WorkingSetBytes: process.WorkingSet64,
            ThreadCount: process.Threads.Count,
            ProcessorCount: Environment.ProcessorCount,
            CpuLoadPercent: cpuLoadPercent is null ? null : Math.Round(cpuLoadPercent.Value, 1),
            TotalDiskBytes: totalDisk,
            FreeDiskBytes: freeDisk,
            DatabaseHealthy: databaseHealthy));
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
