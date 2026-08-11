using Bojan.Api.Auth;
using Bojan.Api.Contracts;
using Bojan.Application.Business;
using Bojan.Domain.Admin;
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

    /// <summary>The stamp folder, kept out of <see cref="AdminFolders"/> — see the route below.</summary>
    private static readonly string[] OwnerFolders = ["invoices"];

    public static void MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/uploads/{folder}", UploadAsCustomer)
            .RequireAuthorization(AuthorizationPolicies.Customer)
            .RequireRateLimiting(RateLimitPolicies.Upload)
            .DisableAntiforgery();

        // Section-gated by the folder rather than by the route, because the
        // folder is what says which part of the panel the file is for. Without
        // it this was the one operator write the permission grid could not
        // reach: a role with the catalogue withdrawn could still put images
        // into it.
        app.MapPost("/admin/uploads/{folder}", UploadAsOperator)
            .RequireAuthorization(AuthorizationPolicies.AdminCatalogue)
            .RequireRateLimiting(RateLimitPolicies.Upload)
            .AddEndpointFilter(new FolderSectionFilter())
            .DisableAntiforgery();

        // The shop's electronic stamp, and owner-only.
        //
        // Not a folder on the route above: that one admits any role the
        // catalogue policy admits, and narrows by section only once the
        // permission grid has been configured — so on an installation that
        // never opened screen 146, a product operator could replace the mark
        // the shop signs its invoices with. The literal segment takes routing
        // precedence over `{folder}`, so this is the handler that runs.
        app.MapPost("/admin/uploads/invoices", UploadStamp)
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireRateLimiting(RateLimitPolicies.Upload)
            .AddEndpointFilter(new SectionPermissionFilter(PanelSection.Settings))
            .DisableAntiforgery();
    }

    private static Task<IResult> UploadStamp(
        IFormFile file, IFileStorage storage, CancellationToken cancellationToken) =>
        Save("invoices", file, storage, OwnerFolders, cancellationToken);

    private static Task<IResult> UploadAsCustomer(
        string folder, IFormFile file, IFileStorage storage, CancellationToken cancellationToken) =>
        Save(folder, file, storage, CustomerFolders, cancellationToken);

    /// <summary>Which part of the panel each operator folder belongs to.</summary>
    private static readonly Dictionary<string, string> FolderSections = new(StringComparer.Ordinal)
    {
        ["products"] = PanelSection.Products,
        ["brands"] = PanelSection.Products,
        ["collections"] = PanelSection.Products,
        ["content"] = PanelSection.Content,
        ["campaigns"] = PanelSection.Campaigns,
    };

    /// <summary>Applies <see cref="SectionPermissionFilter"/> for whichever folder the route named.</summary>
    private sealed class FolderSectionFilter : IEndpointFilter
    {
        public ValueTask<object?> InvokeAsync(
            EndpointFilterInvocationContext context,
            EndpointFilterDelegate next)
        {
            var folder = context.HttpContext.Request.RouteValues["folder"] as string;

            // An unknown folder is refused by the allow-list in Save, which is
            // the check that matters; there is no section to ask about here.
            return folder is not null && FolderSections.TryGetValue(folder, out var section)
                ? new SectionPermissionFilter(section).InvokeAsync(context, next)
                : next(context);
        }
    }

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
        catch (UploadRejectedException rejected)
        {
            // Named, so the panel can tell the operator which of the two it was.
            // Both are the caller's problem rather than a server fault, but "too
            // large" and "not an image" are fixed by different actions and
            // reporting them identically sends people to change the wrong thing.
            return ApiResults.Problem(
                UseCaseError.Invalid,
                rejected.Reason == UploadRejection.Size ? "file-too-large" : "file-type");
        }
    }
}
