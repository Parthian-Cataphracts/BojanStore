using Bojan.Api.Auth;
using Bojan.Application.Administration;
using Bojan.Domain.Admin;
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
            // Every write here is bounded by whatever validator its body has —
            // see ValidationFilter for why this is a group filter rather than a
            // parameter on twenty-three handlers.
            .AddEndpointFilter<ValidationFilter>()
            .NoStore();

        // owner, product — the catalogue and content screens.
        group.MapPost("/products", SaveProduct).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapPost("/products/pricing", UpdatePricing).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapPost("/products/discount", ApplyDiscount).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapPost("/products/variants", SaveVariants).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapPost("/products/skus", SaveSkus).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapPost("/products/attributes", SaveAttributes).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapPost("/categories", SaveCategory).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapPost("/brands", SaveBrand).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapPost("/collections", SaveCollection).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Products);
        group.MapPost("/content", SaveContent).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Content);
        group.MapPost("/campaigns", SaveCampaign).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Campaigns);
        group.MapPost("/inventory/movements", RecordStockMovement).RequireAuthorization(AuthorizationPolicies.AdminCatalogue).RequireSection(PanelSection.Inventory);

        // owner, sales.
        group.MapPost("/coupons", SaveCoupon).RequireAuthorization(AuthorizationPolicies.AdminSales).RequireSection(PanelSection.Campaigns);
        group.MapPost("/business-requests", UpdateBusinessRequest).RequireAuthorization(AuthorizationPolicies.AdminSales).RequireSection(PanelSection.Business);
        group.MapPost("/notifications", QueueBroadcast).RequireAuthorization(AuthorizationPolicies.AdminSales).RequireSection(PanelSection.Campaigns);

        // Under customers, not campaigns: it is a message about one person's own
        // account, written from their record, and an operator who handles
        // customers should be able to send it without also holding the key to
        // every marketing broadcast the shop sends.
        group.MapPost("/customers/notify", NotifyCustomer)
            .RequireAuthorization(AuthorizationPolicies.AdminOrders)
            .RequireSection(PanelSection.Customers);

        // owner, sales, support.
        group.MapPost("/orders/status", UpdateOrderStatus).RequireAuthorization(AuthorizationPolicies.AdminOrders).RequireSection(PanelSection.Orders);

        // Cancelling is not just another status: it moves money and stock, so it
        // is its own endpoint rather than a value the status control can pick.
        group.MapPost("/orders/cancel", CancelOrder).RequireAuthorization(AuthorizationPolicies.AdminOrders).RequireSection(PanelSection.Orders);

        // Recording that an order's money arrived. Owner only, beside the
        // wallet top-up decision and for the same reason: it is a person
        // asserting a payment against a bank statement, and asserting it
        // wrongly means goods leave the building for nothing.
        group.MapPost("/orders/payment", SettleOrderPayment)
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Orders);

        // Owner only. Approving one of these credits spendable balance against
        // a transfer the operator says they saw on a bank statement — the one
        // write in the panel that hands out money rather than changing data.
        group.MapPost("/wallet/topups/decide", DecideWalletTopUp)
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Customers);

        // owner, support.
        group.MapPost("/support/replies", ReplyToThread).RequireAuthorization(AuthorizationPolicies.AdminSupport).RequireSection(PanelSection.Support);
        group.MapPost("/support/canned-replies", SaveCannedReply).RequireAuthorization(AuthorizationPolicies.AdminSupport).RequireSection(PanelSection.Support);
        group.MapPost("/chat/conversations/{visitorId:guid}/reply", ReplyToChat).RequireAuthorization(AuthorizationPolicies.AdminSupport).RequireSection(PanelSection.Support);

        // all roles.
        group.MapPost("/reports/export", QueueReportExport).RequireAuthorization(AuthorizationPolicies.Admin).RequireSection(PanelSection.Reports);
        group.MapPost("/me/password", ChangePassword).RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapPost("/me/2fa", ConfirmTwoFactor).RequireAuthorization(AuthorizationPolicies.Admin);

        // owner only.
        group.MapPost("/settings", SaveSettings).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);
        group.MapPost("/backups", QueueBackup).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);
        group.MapPost("/roles/permissions", SaveRolePermissions).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);
        group.MapPost("/settings/api-keys", SaveApiKey).RequireAuthorization(AuthorizationPolicies.AdminOwner).RequireSection(PanelSection.Settings);
    }

    private static Guid ActorId(ICurrentUser user) =>
        user.AdminId ?? throw new InvalidOperationException(
            "An admin policy authorised a request with no operator id — the policy and CurrentUser disagree.");

    private static async Task<IResult> SaveProduct(
        SaveProductRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        Ok(await catalogue.SaveProductAsync(body, cancellationToken));

    // Screens 106-108. Each takes the product's whole list and replaces it —
    // see the note on SaveVariantsRequest for why these are not per-row.
    private static async Task<IResult> SaveVariants(
        SaveVariantsRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        ApiResults.From(await catalogue.SaveVariantsAsync(body, cancellationToken));

    private static async Task<IResult> SaveSkus(
        SaveSkusRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        ApiResults.From(await catalogue.SaveSkusAsync(body, cancellationToken));

    private static async Task<IResult> SaveAttributes(
        SaveAttributesRequest body, AdminCatalogueService catalogue, CancellationToken cancellationToken) =>
        ApiResults.From(await catalogue.SaveAttributesAsync(body, cancellationToken));

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

    private static async Task<IResult> DecideWalletTopUp(
        WalletTopUpDecisionRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        Guid.TryParse(body.Id, out var id)
            ? ApiResults.From(await operations.DecideWalletTopUpAsync(
                ActorId(user), id, body.Approve, body.Note, cancellationToken))
            : ApiResults.Problem(UseCaseError.Invalid, "id");

    private static async Task<IResult> UpdateOrderStatus(
        OrderStatusRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        ApiResults.From(await operations.UpdateOrderStatusAsync(ActorId(user), body, cancellationToken));

    private static async Task<IResult> SettleOrderPayment(
        OrderPaymentRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        ApiResults.From(await operations.SettleOrderPaymentAsync(ActorId(user), body, cancellationToken));

    private static async Task<IResult> CancelOrder(
        OrderCancellationRequest body,
        Bojan.Application.Orders.OrderCancellationService cancellations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        Guid.TryParse(body.Id, out var id)
            ? ApiResults.From(await cancellations.CancelAsync(
                id,
                ActorId(user),
                // No ownership constraint: an operator cancels anyone's order.
                requireCustomerId: null,
                body.Reason,
                body.ChargePenalty,
                cancellationToken))
            : ApiResults.Problem(UseCaseError.Invalid, "id");

    private static async Task<IResult> UpdateBusinessRequest(
        BusinessRequestUpdate body, AdminOperationsService operations, CancellationToken cancellationToken) =>
        ApiResults.From(await operations.UpdateBusinessRequestAsync(body, cancellationToken));

    private static async Task<IResult> ReplyToThread(
        SupportReplyRequest body, AdminOperationsService operations, CancellationToken cancellationToken) =>
        ApiResults.From(await operations.ReplyToThreadAsync(body, cancellationToken));

    private static async Task<IResult> SaveCannedReply(
        CannedReplyRequest body, AdminOperationsService operations, CancellationToken cancellationToken) =>
        Ok(await operations.SaveCannedReplyAsync(body, cancellationToken));

    private static async Task<IResult> ReplyToChat(
        Guid visitorId,
        LiveChatMessageRequest body,
        Bojan.Application.Support.LiveChatService chat,
        CancellationToken cancellationToken)
    {
        var text = body.Body?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 4000)
        {
            return ApiResults.Problem(UseCaseError.Invalid, "body");
        }

        await chat.SendSupportReplyAsync(visitorId, text, cancellationToken);
        return Results.Ok(await chat.GetConversationAsSupportAsync(visitorId, cancellationToken));
    }

    private static async Task<IResult> QueueBroadcast(
        BroadcastRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        Ok(await operations.QueueBroadcastAsync(ActorId(user), body, cancellationToken));

    private static async Task<IResult> NotifyCustomer(
        CustomerNotificationRequest body,
        AdminOperationsService operations,
        CancellationToken cancellationToken) =>
        ApiResults.From(await operations.NotifyCustomerAsync(body, cancellationToken));

    /// <summary>
    /// The role travels with the actor because the report name is in the body:
    /// a route-level policy gates the endpoint, not the report, and the
    /// owner-only figures have to be refused here or the queue becomes a way
    /// around <c>GET /reports/financial</c>.
    /// </summary>
    private static async Task<IResult> QueueReportExport(
        ReportExportRequest body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        Ok(await operations.QueueReportExportAsync(ActorId(user), user.AdminRole, body, cancellationToken));

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

    private static async Task<IResult> SaveRolePermissions(
        RoleGrantsBody body,
        AdminOperationsService operations,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        ApiResults.From(await operations.SaveRolePermissionsAsync(ActorId(user), body.Grants, cancellationToken));

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
