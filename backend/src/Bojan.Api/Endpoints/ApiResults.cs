using Bojan.Application.Common;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Turns a use case's refusal into an HTTP response.
/// </summary>
/// <remarks>
/// <para>
/// Every failure leaves as RFC 7807 <c>ProblemDetails</c> whose <c>title</c> is
/// a machine key — never a sentence, and never Persian. <c>BACKEND.md</c>
/// section 1.4: "the frontend owns user-facing text", and it does: every screen
/// already has its own Persian copy for these cases and shows it on any
/// non-2xx.
/// </para>
/// <para>
/// <see cref="UseCaseError.NotFound"/> and
/// <see cref="UseCaseError.Forbidden"/> both exist, but ownership failures use
/// <see cref="UseCaseError.NotFound"/> on purpose — <c>BACKEND.md</c> Phase 3:
/// "must 404, not 403, for an order belonging to someone else — a 403 confirms
/// the order exists."
/// </para>
/// </remarks>
public static class ApiResults
{
    public static IResult Problem(UseCaseError error, string? detail)
    {
        var status = error switch
        {
            UseCaseError.NotFound => StatusCodes.Status404NotFound,
            UseCaseError.Conflict => StatusCodes.Status409Conflict,
            UseCaseError.Forbidden => StatusCodes.Status403Forbidden,
            UseCaseError.Unauthorized => StatusCodes.Status401Unauthorized,
            // Out of stock and a rejected coupon are both "the request was
            // understood and refused on the current state of the shop", which
            // is what 409 means — not 400, which would say the body was wrong.
            UseCaseError.OutOfStock => StatusCodes.Status409Conflict,
            UseCaseError.CouponRejected => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Problem(
            title: ToKey(error),
            detail: detail,
            statusCode: status,
            type: $"https://bojan.store/problems/{ToKey(error)}");
    }

    public static IResult From(UseCaseResult result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value, result.Detail);

    public static IResult From<T>(UseCaseResult<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error!.Value, result.Detail);

    /// <summary>A 404 with no body to leak, for a read that found nothing.</summary>
    public static IResult NotFound() => Problem(UseCaseError.NotFound, null);

    /// <summary>kebab-case, stable, and safe to branch on from the frontend.</summary>
    private static string ToKey(UseCaseError error) => error switch
    {
        UseCaseError.NotFound => "not-found",
        UseCaseError.Conflict => "conflict",
        UseCaseError.Forbidden => "forbidden",
        UseCaseError.Unauthorized => "unauthorized",
        UseCaseError.OutOfStock => "out-of-stock",
        UseCaseError.CouponRejected => "coupon-rejected",
        _ => "invalid-request",
    };
}
