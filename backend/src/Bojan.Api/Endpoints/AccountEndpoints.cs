using Bojan.Api.Contracts;
using Bojan.Application.Accounts;
using Bojan.Application.Business;
using Bojan.Application.Contracts;
using Bojan.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Phases 3 and 5 — everything under <c>/me</c>, plus the customer writes the
/// storefront proxies.
/// </summary>
/// <remarks>
/// <para>
/// Every route in this file requires a credential and derives the customer from
/// it. No handler takes a customer id: <see cref="ICurrentUser.CustomerId"/> is
/// the only source, so <c>BACKEND.md</c> section 1.3's "never trust a customer
/// id that arrives in a request body" is a property of the shape rather than a
/// check.
/// </para>
/// <para>
/// <c>Cache-Control: no-store</c> on all of them — these are per-user, and a
/// shared cache holding one shopper's orders is the worst kind of leak.
/// </para>
/// </remarks>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/me").RequireAuthorization(AuthorizationPolicies.Customer).NoStore();

        // --- reads (Phase 3) ---
        group.MapGet(string.Empty, GetProfile);
        group.MapGet("/orders", ListOrders);
        group.MapGet("/orders/{id}", GetOrder);
        group.MapGet("/addresses", ListAddresses);
        group.MapGet("/addresses/{id:guid}", GetAddress);
        group.MapGet("/wishlist", ListWishlist);
        group.MapGet("/returns", ListReturns);
        group.MapGet("/returns/{id}", GetReturn);
        group.MapGet("/notifications", ListNotifications);
        group.MapGet("/support/tickets", ListTickets);
        group.MapGet("/reviews", ListMyReviews);
        group.MapGet("/reviews/awaiting", ListAwaitingReviews);
        group.MapGet("/wallet", GetWallet);
        group.MapGet("/wallet/transactions", ListWalletTransactions);
        group.MapGet("/coupons", ListCoupons);
        group.MapGet("/recently-viewed", ListRecentlyViewed);

        // --- writes (Phase 5) ---
        // Paths are the ones the storefront's allow-list already forwards to —
        // see apps/storefront/src/app/api/account/[action]/route.ts.
        //
        // PUT and POST both, for this route and /business/organization: the
        // Phase 5 table in BACKEND.md writes them as PUT, while the proxy that
        // is the only caller sends POST for every action on its list. Accepting
        // both honours the documented contract without a frontend change, and
        // costs one extra route registration.
        group.MapPut(string.Empty, UpdateProfile);
        group.MapPost(string.Empty, UpdateProfile);
        group.MapPost("/addresses", SaveAddress);
        group.MapPost("/addresses/delete", DeleteAddress);
        group.MapPost("/returns", CreateReturn);

        // The shopper cancelling their own order. Same implementation the panel
        // uses; the customer id is what scopes it to theirs, and the penalty
        // always applies here because by definition they asked.
        group.MapPost("/orders/cancel", CancelOwnOrder);
        group.MapPost("/notifications/read", MarkNotificationsRead);
        group.MapPost("/wishlist/remove", RemoveFromWishlist);
        group.MapPost("/search-history/clear", ClearSearchHistory);
        group.MapPost("/wallet/topup", TopUpWallet);
        group.MapPost("/wallet/topup/confirm", ConfirmTopUp);
        group.MapPost("/wallet/topup/manual", SubmitManualTopUp);

        // Reviews and questions are written against a product, not against the
        // customer's own record, so they keep the paths the proxy uses.
        var root = app.MapGroup(string.Empty).RequireAuthorization(AuthorizationPolicies.Customer).NoStore();
        root.MapPost("/reviews", CreateReview);
        root.MapPost("/questions", CreateQuestion);
        root.MapGet("/business/requests", ListBusinessRequests);
        root.MapGet("/business/requests/{id}", GetBusinessRequest);
        root.MapGet("/business/quotes", ListQuotes);
        root.MapGet("/business/quotes/{id}", GetQuote);
        root.MapPut("/business/organization", SaveOrganization);
        root.MapPost("/business/organization", SaveOrganization);
    }

    private static Guid CustomerId(ICurrentUser user) =>
        user.CustomerId ?? throw new InvalidOperationException(
            "The customer policy authorised a request with no customer id — the policy and CurrentUser disagree.");

    private static async Task<IResult> GetProfile(
        IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        await queries.GetProfileAsync(CustomerId(user), cancellationToken) is { } profile
            ? Results.Ok(profile)
            : ApiResults.NotFound();

    private static async Task<IResult> ListOrders(
        IAccountQueries queries,
        ICurrentUser user,
        CancellationToken cancellationToken,
        [FromQuery] string? status = null) =>
        Results.Ok(await queries.ListOrdersAsync(CustomerId(user), status, cancellationToken));

    /// <summary>
    /// 404, not 403, when the order belongs to someone else — the query is
    /// scoped to the caller, so there is no other answer it could give.
    /// </summary>
    private static async Task<IResult> GetOrder(
        string id, IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        await queries.GetOrderAsync(CustomerId(user), id, cancellationToken) is { } order
            ? Results.Ok(order)
            : ApiResults.NotFound();

    private static async Task<IResult> ListAddresses(
        IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListAddressesAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> GetAddress(
        Guid id, IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        await queries.GetAddressAsync(CustomerId(user), id, cancellationToken) is { } address
            ? Results.Ok(address)
            : ApiResults.NotFound();

    private static async Task<IResult> ListWishlist(
        IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListWishlistAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> ListReturns(
        IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListReturnsAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> GetReturn(
        string id, IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        await queries.GetReturnAsync(CustomerId(user), id, cancellationToken) is { } request
            ? Results.Ok(request)
            : ApiResults.NotFound();

    private static async Task<IResult> ListNotifications(
        IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListNotificationsAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> ListTickets(
        IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListTicketsAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> ListMyReviews(
        IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListMyReviewsAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> ListAwaitingReviews(
        IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListAwaitingReviewsAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> GetWallet(
        AccountService accounts, ICurrentUser user, CancellationToken cancellationToken) =>
        ApiResults.From(await accounts.GetWalletAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> ListWalletTransactions(
        IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListWalletTransactionsAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> ListCoupons(
        IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListCouponsAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> ListRecentlyViewed(
        IAccountQueries queries, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListRecentlyViewedAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> UpdateProfile(
        UpdateProfileBody body,
        IValidator<UpdateProfileBody> validator,
        AccountService accounts,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return ApiResults.From(await accounts.UpdateProfileAsync(
            CustomerId(user),
            new UpdateProfileRequest(
                body.FirstName, body.LastName, body.Email, body.BirthDate, body.City, body.NationalId, body.Avatar),
            cancellationToken));
    }

    private static async Task<IResult> SaveAddress(
        SaveAddressBody body,
        IValidator<SaveAddressBody> validator,
        AccountService accounts,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return ApiResults.From(await accounts.SaveAddressAsync(
            CustomerId(user),
            new SaveAddressRequest(
                Guid.TryParse(body.Id, out var id) ? id : null,
                body.Title,
                body.Recipient,
                body.Phone,
                body.Province,
                body.City,
                body.PostalCode,
                body.Line,
                body.IsDefault ?? false),
            cancellationToken));
    }

    /// <summary>Starts a gateway top-up. Credits nothing — see the confirm below.</summary>
    private static async Task<IResult> TopUpWallet(
        WalletTopUpBody body, AccountService accounts, ICurrentUser user, CancellationToken cancellationToken) =>
        ApiResults.From(await accounts.StartGatewayTopUpAsync(CustomerId(user), body.Amount, cancellationToken));

    /// <summary>
    /// Where the gateway returns the shopper. Idempotent: refreshing it reports
    /// the outcome again rather than crediting again.
    /// </summary>
    private static async Task<IResult> ConfirmTopUp(
        WalletTopUpConfirmBody body,
        AccountService accounts,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await accounts.ConfirmGatewayTopUpAsync(CustomerId(user), body.Reference, cancellationToken));

    /// <summary>Files a card-to-card transfer for an operator to confirm.</summary>
    private static async Task<IResult> SubmitManualTopUp(
        ManualTopUpBody body,
        AccountService accounts,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        ApiResults.From(await accounts.SubmitManualTopUpAsync(
            CustomerId(user),
            new ManualTopUpRequest(
                body.Amount,
                body.TrackingNumber,
                DateOnly.TryParse(body.PaidOn, out var paidOn) ? paidOn : null,
                body.ReceiptUrl,
                body.Note),
            cancellationToken));

    private static async Task<IResult> DeleteAddress(
        IdBody body, AccountService accounts, ICurrentUser user, CancellationToken cancellationToken) =>
        Guid.TryParse(body.Id, out var id)
            ? ApiResults.From(await accounts.DeleteAddressAsync(CustomerId(user), id, cancellationToken))
            : ApiResults.Problem(UseCaseError.Invalid, "id");

    /// <summary>
    /// <c>POST /me/orders/cancel</c> — the shopper cancelling their own order.
    /// </summary>
    /// <remarks>
    /// The penalty is not a parameter and neither is the refund: both are
    /// derived from what the order recorded and how far it got. The customer id
    /// comes from the session rather than the body, so this can only ever reach
    /// the caller's own order, and someone else's answers not-found rather than
    /// forbidden — an order that exists must not be distinguishable from one
    /// that does not.
    /// </remarks>
    private static async Task<IResult> CancelOwnOrder(
        CancelOrderBody body,
        Bojan.Application.Orders.OrderCancellationService cancellations,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        if (user.CustomerId is not { } customerId)
        {
            return ApiResults.Problem(UseCaseError.Unauthorized, null);
        }

        if (!Guid.TryParse(body.OrderId, out var orderId))
        {
            return ApiResults.Problem(UseCaseError.Invalid, "orderId");
        }

        return ApiResults.From(await cancellations.CancelAsync(
            orderId,
            actorId: customerId,
            requireCustomerId: customerId,
            reason: string.IsNullOrWhiteSpace(body.Reason) ? "لغو توسط مشتری" : body.Reason.Trim(),
            // They asked, so the percentage applies wherever the rules say it
            // does. Waiving it is the operator's call, from the panel.
            chargePenalty: true,
            cancellationToken));
    }

    private static async Task<IResult> CreateReturn(
        CreateReturnBody body,
        IValidator<CreateReturnBody> validator,
        AccountService accounts,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var items = body.Items
            .Where(item => Guid.TryParse(item.ProductId, out _))
            .Select(item => new ReturnItemRequest(Guid.Parse(item.ProductId), item.Quantity))
            .ToList();

        if (items.Count != body.Items.Count)
        {
            return ApiResults.Problem(UseCaseError.Invalid, "items");
        }

        return ApiResults.From(await accounts.CreateReturnAsync(
            CustomerId(user),
            new CreateReturnRequest(body.OrderId, items, body.Reason, body.Description, body.RefundMethod),
            cancellationToken));
    }

    /// <summary>
    /// Marks notifications read. An empty <c>ids</c> means "all" — screen 53's
    /// header action posts no ids at all.
    /// </summary>
    /// <summary>
    /// Marks notifications read. An empty list means all of them — screen 53's
    /// header action posts no ids at all.
    /// </summary>
    /// <remarks>
    /// The list is bounded before it becomes a query. Unparseable ids were being
    /// dropped but the count was not checked, so a signed-in caller could post
    /// tens of thousands of them and have every one folded into a single
    /// <c>IN (…)</c> — past PostgreSQL's parameter ceiling, which is a 500 in
    /// answer to a request the screen cannot even produce. Nothing in the panel
    /// or the storefront sends more than a page of notifications' worth.
    /// </remarks>
    private static async Task<IResult> MarkNotificationsRead(
        IdsBody body, AccountService accounts, ICurrentUser user, CancellationToken cancellationToken)
    {
        const int MaxIds = 200;

        if (body.Ids is { Count: > MaxIds })
        {
            return ApiResults.Problem(UseCaseError.Invalid, "ids");
        }

        var ids = (body.Ids ?? [])
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .ToList();

        var updated = await accounts.MarkNotificationsReadAsync(CustomerId(user), ids, cancellationToken);
        return Results.Ok(new { updated });
    }

    private static async Task<IResult> RemoveFromWishlist(
        ProductIdBody body, AccountService accounts, ICurrentUser user, CancellationToken cancellationToken) =>
        Guid.TryParse(body.ProductId, out var productId)
            ? ApiResults.From(await accounts.RemoveFromWishlistAsync(CustomerId(user), productId, cancellationToken))
            : ApiResults.Problem(UseCaseError.Invalid, "productId");

    private static async Task<IResult> ClearSearchHistory(
        AccountService accounts, ICurrentUser user, CancellationToken cancellationToken)
    {
        var removed = await accounts.ClearSearchHistoryAsync(CustomerId(user), cancellationToken);
        return Results.Ok(new { removed });
    }

    private static async Task<IResult> CreateReview(
        CreateReviewBody body,
        IValidator<CreateReviewBody> validator,
        AccountService accounts,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return ApiResults.From(await accounts.CreateReviewAsync(
            CustomerId(user),
            new CreateReviewRequest(body.ProductSlug, body.Rating, body.Title, body.Body, body.Recommend ?? false),
            cancellationToken));
    }

    private static async Task<IResult> CreateQuestion(
        CreateQuestionBody body,
        IValidator<CreateQuestionBody> validator,
        AccountService accounts,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return ApiResults.From(await accounts.CreateQuestionAsync(
            CustomerId(user),
            new CreateQuestionRequest(body.ProductSlug, body.Body),
            cancellationToken));
    }

    private static async Task<IResult> ListBusinessRequests(
        IBusinessQueries business, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await business.ListRequestsAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> GetBusinessRequest(
        string id, IBusinessQueries business, ICurrentUser user, CancellationToken cancellationToken) =>
        await business.GetRequestAsync(CustomerId(user), id, cancellationToken) is { } request
            ? Results.Ok(request)
            : ApiResults.NotFound();

    private static async Task<IResult> ListQuotes(
        IBusinessQueries business, ICurrentUser user, CancellationToken cancellationToken) =>
        Results.Ok(await business.ListQuotesAsync(CustomerId(user), cancellationToken));

    private static async Task<IResult> GetQuote(
        string id, IBusinessQueries business, ICurrentUser user, CancellationToken cancellationToken) =>
        await business.GetQuoteAsync(CustomerId(user), id, cancellationToken) is { } quote
            ? Results.Ok(quote)
            : ApiResults.NotFound();

    private static async Task<IResult> SaveOrganization(
        SaveOrganizationBody body,
        IValidator<SaveOrganizationBody> validator,
        BusinessService business,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return ApiResults.From(await business.SaveOrganizationAsync(
            CustomerId(user),
            new SaveOrganizationRequest(
                body.Organization,
                body.RegistrationNumber,
                body.EconomicCode,
                body.Province,
                body.City,
                body.Address,
                body.Phone,
                body.Email),
            cancellationToken));
    }
}

/// <summary>
/// Marks a group of routes as per-user and uncacheable.
/// </summary>
/// <remarks>
/// <c>BACKEND.md</c> Phase 3: "All are per-user — <c>Cache-Control:
/// no-store</c>." Applied to the group so a route added later inherits it
/// rather than needing someone to remember.
/// </remarks>
internal static class NoStoreExtensions
{
    public static RouteGroupBuilder NoStore(this RouteGroupBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return result;
        });
}
