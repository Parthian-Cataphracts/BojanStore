using Bojan.Application.Administration;
using Bojan.Application.Common;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Phase 7 — the panel's writes, one route per row of <c>BACKEND.md</c>'s
/// table.
/// </summary>
/// <remarks>
/// <para>
/// Each route's path, accepted fields and role gate come from the same row of
/// <c>apps/admin/src/lib/api/resources.ts</c> that the panel enforces against.
/// The panel drops fields the resource does not declare and refuses a role the
/// resource does not list; both are re-enforced here, because a request that
/// never went through the panel gets neither for free.
/// </para>
/// <para>
/// Every one of these audits. The audit row is added to the same change tracker
/// as the change, so <c>SaveChanges</c> commits both or neither — a successful
/// write with no trail is not a state this can reach.
/// </para>
/// </remarks>
public static class AdminWriteEndpoints
{
    public static void MapAdminWriteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(string.Empty)
            .RequireRateLimiting(RateLimitPolicies.AdminWrite)
            .NoStore();

        // owner, product — the catalogue and content screens.
        group.MapPost("/products", SaveProduct).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapPost("/products/pricing", UpdatePricing).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapPost("/products/discount", ApplyDiscount).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapPost("/categories", SaveCategory).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapPost("/brands", SaveBrand).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapPost("/collections", SaveCollection).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapPost("/content", SaveContent).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapPost("/campaigns", SaveCampaign).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);
        group.MapPost("/inventory/movements", RecordStockMovement).RequireAuthorization(AuthorizationPolicies.AdminCatalogue);

        // owner, sales.
        group.MapPost("/coupons", SaveCoupon).RequireAuthorization(AuthorizationPolicies.AdminSales);
        group.MapPost("/business-requests", UpdateBusinessRequest).RequireAuthorization(AuthorizationPolicies.AdminSales);
        group.MapPost("/notifications", QueueBroadcast).RequireAuthorization(AuthorizationPolicies.AdminSales);

        // owner, sales, support.
        group.MapPost("/orders/status", UpdateOrderStatus).RequireAuthorization(AuthorizationPolicies.AdminOrders);

        // owner, support.
        group.MapPost("/support/replies", ReplyToThread).RequireAuthorization(AuthorizationPolicies.AdminSupport);
        group.MapPost("/support/canned-replies", SaveCannedReply).RequireAuthorization(AuthorizationPolicies.AdminSupport);

        // all roles.
        group.MapPost("/reports/export", QueueReportExport).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapPost("/me/password", ChangePassword).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapPost("/me/2fa", ConfirmTwoFactor).RequireAuthorization(AuthorizationPolicies.Admin);

        // owner only.
        group.MapPost("/settings", SaveSettings).RequireAuthorization(AuthorizationPolicies.AdminOwner);
        group.MapPost("/backups", QueueBackup).RequireAuthorization(AuthorizationPolicies.AdminOwner);
        group.MapPost("/settings/api-keys", SaveApiKey).RequireAuthorization(AuthorizationPolicies.AdminOwner);
    }

    private static Guid ActorId(ICurrentUser user) =>
        user.AdminId ?? throw new InvalidOperationException(
            "An admin policy authorised a request with no operator id — the policy and CurrentUser disagree.");

    private static async Task<IResult> SaveProduct(
        SaveProductRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        Ok(await catalogue.SaveProductAsync(body, cancellationToken));

    private static async Task<IResult> UpdatePricing(
        ProductPricingRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        ApiResults.From(await catalogue.UpdatePricingAsync(body, cancellationToken));

    private static async Task<IResult> ApplyDiscount(
        ProductDiscountRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        ApiResults.From(await catalogue.ApplyDiscountAsync(body, cancellationToken));

    private static async Task<IResult> SaveCategory(
        SaveCategoryRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        Ok(await catalogue.SaveCategoryAsync(body, cancellationToken));

    private static async Task<IResult> SaveBrand(
        SaveBrandRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        Ok(await catalogue.SaveBrandAsync(body, cancellationToken));

    private static async Task<IResult> SaveCollection(
        SaveCollectionRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        Ok(await catalogue.SaveCollectionAsync(body, cancellationToken));

    private static async Task<IResult> SaveContent(
        SaveContentRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        Ok(await catalogue.SaveContentAsync(body, cancellationToken));

    private static async Task<IResult> SaveCampaign(
        SaveCampaignRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        Ok(await catalogue.SaveCampaignAsync(body, cancellationToken));

    private static async Task<IResult> SaveCoupon(
        SaveCouponRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        Ok(await catalogue.SaveCouponAsync(body, cancellationToken));

    private static async Task<IResult> RecordStockMovement(
        StockMovementRequest body,
        AdminCatalogueService catalogue,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        ApiResults.From(await catalogue.RecordStockMovementAsync(ActorId(user), body, cancellationToken));

    private static async Task<IResult> UpdateOrderStatus(
        OrderStatusRequest body, AdminOperationsService operations, CancellationToken cancellationToken) =>
        ApiResults.From(await operations.UpdateOrderStatusAsync(body, cancellationToken));

    private static async Task<IResult> UpdateBusinessRequest(
        BusinessRequestUpdate body, AdminOperationsService operations, CancellationToken cancellationToken) =>
        ApiResults.From(await operations.UpdateBusinessRequestAsync(body, cancellationToken));

    private static async Task<IResult> ReplyToThread(
        SupportReplyRequest body, AdminOperationsService operations, CancellationToken cancellationToken) =>
        ApiResults.From(await operations.ReplyToThreadAsync(body, cancellationToken));

    private static async Task<IResult> SaveCannedReply(
        CannedReplyRequest body, AdminOperationsService operations, CancellationToken cancellationToken) =>
        Ok(await operations.SaveCannedReplyAsync(body, cancellationToken));

    private static async Task<IResult> QueueBroadcast(
        BroadcastRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        Ok(await operations.QueueBroadcastAsync(ActorId(user), body, cancellationToken));

    private static async Task<IResult> QueueReportExport(
        ReportExportRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        Ok(await operations.QueueReportExportAsync(ActorId(user), body, cancellationToken));

    private static async Task<IResult> SaveSettings(
        SettingsRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        ApiResults.From(await operations.SaveSettingsAsync(ActorId(user), body, cancellationToken));

    private static async Task<IResult> QueueBackup(
        BackupRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        Ok(await operations.QueueBackupAsync(ActorId(user), body, cancellationToken));

    /// <summary>
    /// Creating a key returns its plaintext once; updating one returns nothing.
    /// </summary>
    private static async Task<IResult> SaveApiKey(
        ApiKeyRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var result = await operations.SaveApiKeyAsync(ActorId(user), body, cancellationToken);

        if (!result.IsSuccess)
        {
            return ApiResults.Problem(result.Error!.Value, result.Detail);
        }

        return result.Value is { } created ? Results.Ok(created) : Results.NoContent();
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        ApiResults.From(await operations.ChangePasswordAsync(ActorId(user), body, cancellationToken));

    private static async Task<IResult> ConfirmTwoFactor(
        TwoFactorRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        ApiResults.From(await operations.ConfirmTwoFactorAsync(ActorId(user), body, cancellationToken));

    /// <summary>
    /// A saved entity's id, in the shape the panel's forms read back
    /// (<c>{ id }</c>), or the failure as ProblemDetails.
    /// </summary>
    private static IResult Ok(UseCaseResult<string> result) =>
        result.IsSuccess
            ? Results.Ok(new { id = result.Value })
            : ApiResults.Problem(result.Error!.Value, result.Detail);
}
