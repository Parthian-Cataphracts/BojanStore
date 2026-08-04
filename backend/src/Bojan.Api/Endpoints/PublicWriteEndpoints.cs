using Bojan.Api.Contracts;
using Bojan.Application.Business;
using Bojan.Application.Catalogue;
using Bojan.Application.Common;
using Bojan.Application.Support;
using Microsoft.AspNetCore.Mvc;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Phase 5's public half — the writes a visitor with no account may make.
/// </summary>
/// <remarks>
/// <para>
/// These four are the ones the frontend's allow-list marks
/// <c>private: false</c>: a stock alert, a message to support, and the two B2B
/// forms. A signed-in customer's id is attached when there is one, purely so
/// the result appears in their own history — it is never required.
/// </para>
/// <para>
/// Rate limits here are the tightest in the API, matching the frontend's own
/// (3-5 per minute). An unauthenticated write endpoint is the cheapest thing in
/// a system to abuse.
/// </para>
/// </remarks>
public static class PublicWriteEndpoints
{
    public static void MapPublicWriteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(string.Empty).AllowAnonymous();

        group.MapPost("/stock-alerts", RequestStockAlert).RequireRateLimiting(RateLimitPolicies.PublicWrite);
        group.MapPost("/support/messages", SubmitContactMessage).RequireRateLimiting(RateLimitPolicies.PublicWrite);
        group.MapPost("/business/requests", CreateBusinessRequest).RequireRateLimiting(RateLimitPolicies.PublicWrite);
        group.MapPost("/business/bulk-orders", CreateBulkOrder).RequireRateLimiting(RateLimitPolicies.PublicWrite);

        group.MapGet("/business/gift-bundles", ListGiftBundles);
    }

    private static async Task<IResult> RequestStockAlert(
        StockAlertBody body,
        FluentValidation.IValidator<StockAlertBody> validator,
        SupportService support,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return ApiResults.From(await support.RequestStockAlertAsync(
            new StockAlertRequest(body.ProductSlug, body.Phone, body.Email), cancellationToken));
    }

    private static async Task<IResult> SubmitContactMessage(
        ContactMessageBody body,
        FluentValidation.IValidator<ContactMessageBody> validator,
        SupportService support,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var id = await support.SubmitContactMessageAsync(
            user.CustomerId,
            new ContactMessageRequest(body.Name, body.Phone, body.Email, body.Subject, body.Body),
            cancellationToken);

        return Results.Ok(new { id = id.ToString() });
    }

    private static async Task<IResult> CreateBusinessRequest(
        BusinessRequestBody body,
        FluentValidation.IValidator<BusinessRequestBody> validator,
        BusinessService business,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return Results.Ok(await business.CreateRequestAsync(
            user.CustomerId,
            new CreateBusinessRequest(
                body.Organization, body.Contact, body.Phone, body.Email, body.Items, body.Description, body.Deadline),
            cancellationToken));
    }

    private static async Task<IResult> CreateBulkOrder(
        BulkOrderBody body,
        FluentValidation.IValidator<BulkOrderBody> validator,
        BusinessService business,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return Results.Ok(await business.CreateBulkOrderAsync(
            user.CustomerId,
            new CreateBulkOrderRequest(
                body.Organization, body.Contact, body.Phone, body.Email, body.Items, body.Note),
            cancellationToken));
    }

    private static async Task<IResult> ListGiftBundles(
        IBusinessQueries business,
        CancellationToken cancellationToken,
        [FromQuery] string? category = null) =>
        Results.Ok(await business.ListGiftBundlesAsync(category, cancellationToken));
}

/// <summary>
/// Phase 8 — the uploads six disabled controls are waiting on.
/// </summary>
/// <remarks>
/// One endpoint, not six. <c>BACKEND.md</c> Phase 8: "Uploads are one shared
/// decision, not six. Doing it once unblocks all of them." The folder says what
/// the file is for; the storage adapter decides where it lands and enforces the
/// type and size limits.
/// </remarks>
public static class UploadEndpoints
{
    // `receipts` is where a card-to-card top-up's proof of transfer goes. Its
    // absence was why that flow could not work honestly: the request requires a
    // receipt, so with no folder to upload one into the only way to satisfy it
    // was a URL on somebody else's host.
    private static readonly string[] CustomerFolders = ["avatars", "returns", "business", "receipts"];
    private static readonly string[] AdminFolders = ["products", "brands", "collections", "content", "campaigns"];

    public static void MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/uploads/{folder}", UploadAsCustomer)
            .RequireAuthorization(AuthorizationPolicies.Customer)
            .RequireRateLimiting(RateLimitPolicies.Upload)
            .DisableAntiforgery();

        app.MapPost("/admin/uploads/{folder}", UploadAsOperator)
            .RequireAuthorization(AuthorizationPolicies.AdminCatalogue)
            .RequireRateLimiting(RateLimitPolicies.Upload)
            .DisableAntiforgery();
    }

    private static Task<IResult> UploadAsCustomer(
        string folder, IFormFile file, IFileStorage storage, CancellationToken cancellationToken) =>
        Save(folder, file, storage, CustomerFolders, cancellationToken);

    private static Task<IResult> UploadAsOperator(
        string folder, IFormFile file, IFileStorage storage, CancellationToken cancellationToken) =>
        Save(folder, file, storage, AdminFolders, cancellationToken);

    /// <summary>
    /// The folder is checked against a fixed list rather than sanitised.
    /// </summary>
    /// <remarks>
    /// An allow-list, because a customer must not be able to write into the
    /// folder the product images live in, and because "sanitise the path" is
    /// the check everyone believes they got right.
    /// </remarks>
    private static async Task<IResult> Save(
        string folder,
        IFormFile file,
        IFileStorage storage,
        IReadOnlyList<string> allowed,
        CancellationToken cancellationToken)
    {
        if (!allowed.Contains(folder, StringComparer.Ordinal))
        {
            return ApiResults.Problem(UseCaseError.Invalid, "folder");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var url = await storage.SaveAsync(folder, file.FileName, file.ContentType, stream, cancellationToken);
            return Results.Ok(new { url });
        }
        catch (InvalidOperationException)
        {
            // The storage adapter refuses on type, size or a content mismatch.
            // All three are the caller's problem, not a server fault.
            return ApiResults.Problem(UseCaseError.Invalid, "file");
        }
    }
}
