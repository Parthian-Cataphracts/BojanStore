using Bojan.Api.Contracts;
using Bojan.Application.Auth;
using FluentValidation;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Phase 1's three endpoints, mounted where the two frontends actually call
/// them.
/// </summary>
/// <remarks>
/// The storefront's base URL is <c>/api</c> and the panel's is
/// <c>/api/admin</c> (see each app's <c>.env.example</c>), so
/// <c>POST /auth/otp/request</c> from the storefront is
/// <c>/api/auth/otp/request</c> here, and <c>POST /auth/login</c> from the
/// panel is <c>/api/admin/auth/login</c>. They are registered from two methods
/// for that reason, not because the code differs.
/// </remarks>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").AllowAnonymous();

        group.MapPost("/otp/request", RequestOtp)
            .RequireRateLimiting(RateLimitPolicies.OtpRequest);

        group.MapPost("/otp/verify", VerifyOtp)
            .RequireRateLimiting(RateLimitPolicies.OtpVerify);

        // The password door. Rate-limited on the same policies the code path
        // uses: registering and asking for a reset both send a message, and
        // signing in is guessable, so all three need a ceiling per caller.
        group.MapPost("/register", Register)
            .RequireRateLimiting(RateLimitPolicies.OtpRequest);

        group.MapPost("/login", CustomerLogin)
            .RequireRateLimiting(RateLimitPolicies.OtpVerify);

        group.MapPost("/forgot-password", ForgotPassword)
            .RequireRateLimiting(RateLimitPolicies.OtpRequest);

        group.MapPost("/reset-password", ResetPassword)
            .RequireRateLimiting(RateLimitPolicies.OtpVerify);
    }

    public static void MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", AdminLogin)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.AdminLogin);
    }

    /// <summary>
    /// <c>POST /api/auth/otp/request</c> —
    /// <c>apps/storefront/src/app/api/auth/otp/request/route.ts</c> sends
    /// <c>{ phone }</c> already normalised to ASCII digits and ignores the
    /// response body entirely, so this always answers <c>204</c> on success. It
    /// never reveals whether the number is already registered — the frontend
    /// deliberately does not ask.
    /// </summary>
    private static async Task<IResult> RequestOtp(
        OtpRequestBody body,
        IValidator<OtpRequestBody> validator,
        AuthService auth,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        await auth.RequestOtpAsync(body.Phone, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// <c>POST /api/auth/otp/verify</c> — matches
    /// <c>apps/storefront/src/app/api/auth/otp/verify/route.ts</c>, which reads
    /// <c>{ userId, firstName?, lastName?, isNewUser? }</c> from this call and
    /// then mints its own session cookie. <c>token</c> is additive: today's
    /// frontend ignores it, and forwarding it as <c>Authorization: Bearer</c>
    /// on later calls is the one frontend change <c>BACKEND.md</c> section 1.3
    /// (option b) still expects.
    /// </summary>
    private static async Task<IResult> VerifyOtp(
        OtpVerifyBody body,
        IValidator<OtpVerifyBody> validator,
        AuthService auth,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var (result, failure) = await auth.VerifyOtpAsync(body.Phone, body.Code, cancellationToken);

        if (failure is not null)
        {
            // Machine keys, not sentences — BACKEND.md section 1.4: the
            // frontend owns the Persian copy, and the sign-in screen already
            // has its own for each of these.
            var (title, status) = failure switch
            {
                OtpVerifyFailure.NoActiveChallenge or OtpVerifyFailure.Expired =>
                    ("otp-expired", StatusCodes.Status400BadRequest),
                OtpVerifyFailure.TooManyAttempts =>
                    ("otp-attempts-exhausted", StatusCodes.Status429TooManyRequests),
                _ => ("otp-incorrect", StatusCodes.Status400BadRequest),
            };

            return Results.Problem(title: title, statusCode: status);
        }

        var response = new OtpVerifyResponse(
            result!.CustomerId.ToString(),
            result.FirstName,
            result.LastName,
            result.IsNewUser,
            result.Token);

        return Results.Ok(response);
    }

    // --- the password door ---------------------------------------------------

    /// <summary>
    /// <c>POST /api/auth/register</c> — phone, email and password.
    /// </summary>
    /// <remarks>
    /// Answers with the same <see cref="OtpVerifyResponse"/> shape the code path
    /// does, so the frontend's session-minting logic is one branch rather than
    /// two: whichever door the shopper came through, the route handler that
    /// receives this already knows what to do with it.
    /// </remarks>
    private static async Task<IResult> Register(
        RegisterBody body,
        IValidator<RegisterBody> validator,
        CustomerPasswordService passwords,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var result = await passwords.RegisterAsync(
            new RegisterRequest(body.Phone, body.Email, body.Password), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(Describe(result.Value!))
            : ApiResults.Problem(result.Error!.Value, result.Detail);
    }

    /// <summary>
    /// <c>POST /api/auth/login</c> — phone or email, plus a password.
    /// </summary>
    /// <remarks>
    /// One 401 for every way this can fail. The service does not distinguish an
    /// unknown identity from a wrong password, and neither does this: the
    /// difference is exactly what someone enumerating the shop's customers
    /// would be looking for.
    /// </remarks>
    private static async Task<IResult> CustomerLogin(
        PasswordLoginBody body,
        IValidator<PasswordLoginBody> validator,
        CustomerPasswordService passwords,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var result = await passwords.LoginAsync(
            new PasswordLoginRequest(body.Identity, body.Password), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(Describe(result.Value!))
            : Results.Problem(title: "invalid-credentials", statusCode: StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// <c>POST /api/auth/forgot-password</c>. Always 204.
    /// </summary>
    /// <remarks>
    /// Answering differently for an address the shop knows would make this a
    /// way to test whether any given person shops here. The mail is sent only
    /// when there is an account; the response is the same either way.
    /// </remarks>
    private static async Task<IResult> ForgotPassword(
        ForgotPasswordBody body,
        IValidator<ForgotPasswordBody> validator,
        CustomerPasswordService passwords,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        await passwords.RequestResetAsync(body.Email, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordBody body,
        IValidator<ResetPasswordBody> validator,
        CustomerPasswordService passwords,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var result = await passwords.ResetPasswordAsync(
            new ResetPasswordRequest(body.Token, body.Password), cancellationToken);

        return result.IsSuccess ? Results.NoContent() : ApiResults.Problem(result.Error!.Value, result.Detail);
    }

    private static OtpVerifyResponse Describe(CustomerAuthResult result) => new(
        result.CustomerId.ToString(),
        result.FirstName,
        result.LastName,
        result.IsNewUser,
        result.Token,
        result.Phone);

    /// <summary>
    /// <c>POST /api/admin/auth/login</c> — matches
    /// <c>apps/admin/src/app/api/admin-auth/login/route.ts</c>'s non-mock
    /// branch exactly: same request shape, and one rejection for every failure
    /// reason so a wrong password and an unknown account are indistinguishable
    /// from outside.
    /// </summary>
    private static async Task<IResult> AdminLogin(
        AdminLoginBody body,
        IValidator<AdminLoginBody> validator,
        AdminAuthService auth,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            // Deliberately not a 400 with field errors: telling a caller that
            // the password was too short still tells them the identity was
            // shaped like a real one.
            return Results.Problem(title: "credentials-rejected", statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await auth.LoginAsync(body.Identity, body.Password, cancellationToken);
        if (result is null)
        {
            return Results.Problem(title: "credentials-rejected", statusCode: StatusCodes.Status401Unauthorized);
        }

        var response = new AdminLoginResponse(
            result.Id.ToString(),
            result.Name,
            result.Email,
            result.Role,
            result.RequiresTwoFactor ? true : null,
            result.Token);

        return Results.Ok(response);
    }
}
