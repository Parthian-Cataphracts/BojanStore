using Bojan.Api.Contracts;
using Bojan.Application.Checkout;
using Bojan.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Phase 4 — the money path.
/// </summary>
/// <remarks>
/// Two writes and one public read. The writes derive the customer from the
/// credential, re-price everything from the database, and honour an
/// <c>Idempotency-Key</c>; the read is rate-limited hard because it is the one
/// endpoint here that anyone may call.
/// </remarks>
public static class CheckoutEndpoints
{
    /// <summary>
    /// The header the checkout should send so a double-submitted order becomes
    /// one order.
    /// </summary>
    /// <remarks>
    /// <c>BACKEND.md</c> Phase 4, rule 7: "A double-submitted order is the
    /// single worst bug this system can have." The shipped checkout does not
    /// send this header yet, so a key is derived from the basket when it is
    /// absent — see <see cref="IdempotencyKeyFor"/>.
    /// </remarks>
    private const string IdempotencyHeader = "Idempotency-Key";

    public static void MapCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(string.Empty).RequireAuthorization(AuthorizationPolicies.Customer);

        group.MapPost("/cart/coupon", ValidateCoupon).RequireRateLimiting(RateLimitPolicies.Coupon);
        group.MapPost("/orders", PlaceOrder).RequireRateLimiting(RateLimitPolicies.PlaceOrder);

        app.MapGet("/orders/track", TrackOrder)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.OrderTracking);
    }

    private static Guid CustomerId(ICurrentUser user) =>
        user.CustomerId ?? throw new InvalidOperationException(
            "The customer policy authorised a request with no customer id — the policy and CurrentUser disagree.");

    private static async Task<IResult> ValidateCoupon(
        CouponBody body,
        IValidator<CouponBody> validator,
        CheckoutService checkout,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var lines = ParseLines(body.Lines);
        if (lines is null)
        {
            return ApiResults.Problem(UseCaseError.Invalid, "lines");
        }

        // An invalid or expired code is a non-2xx, never a `{ valid: false }`
        // body — BACKEND.md Phase 4, and the frontend's ApiError path depends
        // on it.
        return ApiResults.From(await checkout.ValidateCouponAsync(
            CustomerId(user), body.Code, lines, cancellationToken));
    }

    private static async Task<IResult> PlaceOrder(
        PlaceOrderBody body,
        IValidator<PlaceOrderBody> validator,
        CheckoutService checkout,
        ICurrentUser user,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var lines = ParseLines(body.Lines);
        if (lines is null || !Guid.TryParse(body.AddressId, out var addressId))
        {
            return ApiResults.Problem(UseCaseError.Invalid, lines is null ? "lines" : "addressId");
        }

        var request = new PlaceOrderRequest(
            lines,
            addressId,
            body.ShippingMethodId,
            body.PaymentMethodId,
            body.CouponCode,
            body.Note,
            IdempotencyKeyFor(http, CustomerId(user), body),
            body.DeliveryWindow);

        var result = await checkout.PlaceOrderAsync(CustomerId(user), request, cancellationToken);
        return ApiResults.From(result);
    }

    /// <summary>
    /// The submitted <c>Idempotency-Key</c>, or one derived from the basket
    /// itself.
    /// </summary>
    /// <remarks>
    /// The derived key is the customer plus the exact basket, address and
    /// method choice. It makes a genuine double-submit — the same order posted
    /// twice because a button was tapped twice — collapse into one order
    /// without the frontend having to change. It also means a shopper who
    /// deliberately re-orders the identical basket gets the first order back
    /// instead of a second one, which is the trade-off of inferring a key
    /// rather than being given one, and the reason the header remains the
    /// supported way to do this.
    /// </remarks>
    private static string IdempotencyKeyFor(HttpContext http, Guid customerId, PlaceOrderBody body)
    {
        var submitted = http.Request.Headers[IdempotencyHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(submitted))
        {
            return submitted.Length <= 200 ? submitted : submitted[..200];
        }

        // SkuId is part of the key for the same reason ProductId is: two
        // lines that differ only in which variant was chosen are two
        // different baskets, and folding them into one string here would
        // make the second order collapse into the first's idempotency row —
        // "already placed" for an order that was never placed at all.
        var basket = string.Join(
            '|',
            body.Lines
                .OrderBy(line => line.ProductId, StringComparer.Ordinal)
                .ThenBy(line => line.SkuId, StringComparer.Ordinal)
                .Select(line => $"{line.ProductId}:{line.SkuId}x{line.Quantity}"));

        var material = $"{customerId}:{basket}:{body.AddressId}:{body.ShippingMethodId}:{body.PaymentMethodId}:{body.CouponCode}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(material));

        return $"derived:{Convert.ToHexStringLower(hash)}";
    }

    /// <summary>
    /// The public tracking lookup.
    /// </summary>
    /// <remarks>
    /// Both parameters are required and both must match. <c>BACKEND.md</c>
    /// Phase 4: "Matching on number <em>and</em> phone is what stops it being
    /// an order-number enumeration vector; do not let a number alone return
    /// anything." The rate limit on top is the second half of that.
    ///
    /// Not wired on the frontend yet — screen 30 carries a <c>TODO</c> in
    /// <c>components/status/TrackOrderForm.tsx</c>, and finishing it is a small
    /// frontend change this endpoint is waiting on rather than the other way
    /// round.
    /// </remarks>
    private static async Task<IResult> TrackOrder(
        CheckoutService checkout,
        CancellationToken cancellationToken,
        [FromQuery] string? number = null,
        [FromQuery] string? phone = null)
    {
        if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(phone))
        {
            return ApiResults.Problem(UseCaseError.Invalid, "number-and-phone");
        }

        return await checkout.TrackAsync(number, phone, cancellationToken) is { } order
            ? Results.Ok(order)
            : ApiResults.NotFound();
    }

    /// <summary>Null when any product id is not a GUID — a malformed basket, not an empty one.</summary>
    private static IReadOnlyList<OrderLineRequest>? ParseLines(IReadOnlyList<OrderLineBody>? lines)
    {
        if (lines is null)
        {
            return [];
        }

        var parsed = new List<OrderLineRequest>(lines.Count);

        foreach (var line in lines)
        {
            if (!Guid.TryParse(line.ProductId, out var productId))
            {
                return null;
            }

            // A SkuId that fails to parse is dropped rather than rejecting the
            // whole basket — CheckoutService treats an absent SkuId as "no
            // variant", the same as a product that was never given one.
            Guid? skuId = Guid.TryParse(line.SkuId, out var parsedSkuId) ? parsedSkuId : null;

            parsed.Add(new OrderLineRequest(productId, line.Quantity, skuId));
        }

        return parsed;
    }
}
