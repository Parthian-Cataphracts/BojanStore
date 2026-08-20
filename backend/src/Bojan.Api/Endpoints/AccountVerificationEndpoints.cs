using System.Globalization;
using Bojan.Api.Contracts;
using Bojan.Application.Auth;
using Bojan.Application.Common;
using FluentValidation;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Link-based email verification and code-based phone verification for the
/// signed-in customer, plus changing the account's phone number — which is
/// only possible through this verification flow, since a number does not take
/// effect until it has been proven.
/// </summary>
public static class AccountVerificationEndpoints
{
    public static void MapAccountVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/account").RequireAuthorization(AuthorizationPolicies.Customer).NoStore();

        group.MapPost("/email/verify/request", RequestEmailVerification)
            .RequireRateLimiting(RateLimitPolicies.OtpRequest);

        group.MapPost("/phone/verify/request", RequestPhoneVerification)
            .RequireRateLimiting(RateLimitPolicies.OtpRequest);

        group.MapPost("/phone/change/request", RequestPhoneChange)
            .RequireRateLimiting(RateLimitPolicies.OtpRequest);

        group.MapPost("/phone/verify/confirm", ConfirmPhoneVerification)
            .RequireRateLimiting(RateLimitPolicies.OtpVerify);

        // Public: the token in the query string is what proves identity here,
        // the same reasoning CustomerPasswordService.ResetPasswordAsync's link
        // rests on — there is no session yet when a customer opens their inbox.
        app.MapGet("/account/email/verify/confirm", ConfirmEmailVerification)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.OtpVerify);
    }

    private static Guid CustomerId(ICurrentUser user) =>
        user.CustomerId ?? throw new InvalidOperationException(
            "The customer policy authorised a request with no customer id — the policy and CurrentUser disagree.");

    private static async Task<IResult> RequestEmailVerification(
        EmailVerificationService verification,
        ICurrentUser user,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await verification.RequestAsync(CustomerId(user), cancellationToken);

        if (result.Failure == EmailVerificationRequestFailure.NoEmail)
        {
            return ApiResults.Problem(UseCaseError.Invalid, "no-email");
        }

        if (!result.Sent)
        {
            httpContext.Response.Headers.RetryAfter =
                result.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            return Results.Problem(
                title: "email-verification-cooldown",
                statusCode: StatusCodes.Status429TooManyRequests,
                type: "https://bojan.store/problems/email-verification-cooldown",
                extensions: new Dictionary<string, object?> { ["retryAfterSeconds"] = result.RetryAfterSeconds });
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmEmailVerification(
        string? token, EmailVerificationService verification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ApiResults.Problem(UseCaseError.Invalid, "token");
        }

        var confirmed = await verification.ConfirmAsync(token, cancellationToken);
        return confirmed ? Results.NoContent() : ApiResults.Problem(UseCaseError.Invalid, "invalid-token");
    }

    /// <summary>Sends a code to the account's own current phone — no new number involved.</summary>
    private static async Task<IResult> RequestPhoneVerification(
        PhoneVerificationService verification,
        ICurrentUser user,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        await RunRequest(verification, CustomerId(user), targetPhone: null, httpContext, cancellationToken);

    /// <summary>Starts changing the account's phone: sends a code to the candidate number, which is applied only once that code is confirmed.</summary>
    private static async Task<IResult> RequestPhoneChange(
        RequestPhoneChangeBody body,
        IValidator<RequestPhoneChangeBody> validator,
        PhoneVerificationService verification,
        ICurrentUser user,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return await RunRequest(verification, CustomerId(user), body.NewPhone, httpContext, cancellationToken);
    }

    private static async Task<IResult> RunRequest(
        PhoneVerificationService verification,
        Guid customerId,
        string? targetPhone,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await verification.RequestAsync(customerId, targetPhone, cancellationToken);

        if (result.Failure == PhoneVerificationRequestFailure.PhoneTaken)
        {
            return ApiResults.Problem(UseCaseError.Conflict, "phone-taken");
        }

        if (!result.Sent)
        {
            httpContext.Response.Headers.RetryAfter =
                result.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            return Results.Problem(
                title: "phone-verification-cooldown",
                statusCode: StatusCodes.Status429TooManyRequests,
                type: "https://bojan.store/problems/phone-verification-cooldown",
                extensions: new Dictionary<string, object?> { ["retryAfterSeconds"] = result.RetryAfterSeconds });
        }

        return Results.NoContent();
    }

    /// <summary>Confirms whichever pending phone-verification challenge — the account's own number, or a candidate it is changing to — is active for the caller.</summary>
    private static async Task<IResult> ConfirmPhoneVerification(
        PhoneVerifyConfirmBody body,
        IValidator<PhoneVerifyConfirmBody> validator,
        PhoneVerificationService verification,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var (success, failure) = await verification.ConfirmAsync(CustomerId(user), body.Code, cancellationToken);

        if (success)
        {
            return Results.NoContent();
        }

        var (title, status) = failure switch
        {
            PhoneVerificationConfirmFailure.NoActiveChallenge or PhoneVerificationConfirmFailure.Expired =>
                ("phone-verification-expired", StatusCodes.Status400BadRequest),
            PhoneVerificationConfirmFailure.TooManyAttempts =>
                ("phone-verification-attempts-exhausted", StatusCodes.Status429TooManyRequests),
            PhoneVerificationConfirmFailure.PhoneTaken =>
                ("phone-taken", StatusCodes.Status409Conflict),
            _ => ("phone-verification-incorrect", StatusCodes.Status400BadRequest),
        };

        return Results.Problem(title: title, statusCode: status);
    }
}
