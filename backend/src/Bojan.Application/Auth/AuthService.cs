using System.Security.Cryptography;
using System.Text;
using Bojan.Application.Common;
using Bojan.Domain.Customers;
using Bojan.Domain.Identity;

namespace Bojan.Application.Auth;

/// <summary>Result of <see cref="AuthService.VerifyOtpAsync"/> — the exact shape <c>apps/storefront/.../otp/verify/route.ts</c> expects back.</summary>
public sealed record OtpVerifyResult(
    Guid CustomerId,
    string? FirstName,
    string? LastName,
    bool IsNewUser,
    string Token,
    Guid SecurityStamp);

/// <summary>
/// Result of <see cref="AuthService.RequestOtpAsync"/>.
/// </summary>
/// <remarks>
/// <see cref="RetryAfterSeconds"/> is only meaningful when <see cref="Sent"/>
/// is false — how long until <see cref="OtpChallenge.BlocksResend"/> stops
/// saying so for this phone, rounded up so a shopper who waits exactly that
/// long never lands one second short.
/// </remarks>
public sealed record OtpRequestResult(bool Sent, int RetryAfterSeconds = 0)
{
    public static readonly OtpRequestResult Success = new(true);

    public static OtpRequestResult MustWait(int seconds) => new(false, seconds);
}

public enum OtpVerifyFailure
{
    NoActiveChallenge,
    Expired,
    WrongCode,
    TooManyAttempts,

    /// <summary>
    /// The account is suspended.
    /// </summary>
    /// <remarks>
    /// Its own failure rather than a wrong-code answer. Someone whose account
    /// an operator has suspended is not someone who mistyped, and telling them
    /// the code was wrong sends them round the loop forever — the shop wants
    /// them to ring support, which they will only do if the screen says so.
    /// </remarks>
    Blocked,
}

/// <summary>
/// Storefront sign-in: phone + SMS code, no password. Implements
/// <c>POST /auth/otp/request</c> and <c>POST /auth/otp/verify</c> exactly as
/// the frontend already calls them (see <c>BACKEND.md</c> section 1.3 and
/// <c>apps/storefront/src/components/auth/LoginForm.tsx</c>).
/// </summary>
public sealed class AuthService(
    ICustomerRepository customers,
    IOtpChallengeStore challenges,
    ISmsSender sms,
    IJwtTokenGenerator tokens,
    IOtpCodeGenerator codes,
    IDateTimeProvider clock)
{
    /// <summary>
    /// Sends a fresh sign-in code, unless the phone already has a live one.
    /// </summary>
    /// <remarks>
    /// <see cref="IOtpChallengeStore.CreateAsync"/> upserts — nothing there
    /// stopped a second call moments after the first from overwriting a code a
    /// shopper had not finished reading yet, and nothing stopped the same call
    /// happening every few seconds by resubmitting the phone step or reloading
    /// mid-flow. The check is against <see cref="OtpChallenge.BlocksResend"/>
    /// on the stored row rather than anything the caller sends, which is what
    /// makes it survive a reload: there is no client-held state for a fresh
    /// page to lose.
    /// </remarks>
    public async Task<OtpRequestResult> RequestOtpAsync(string phone, CancellationToken cancellationToken)
    {
        var existing = await challenges.FindActiveAsync(phone, OtpPurpose.SignIn, cancellationToken);
        if (existing is not null && existing.BlocksResend(clock.UtcNow))
        {
            var remaining = existing.ExpiresAtUtc - clock.UtcNow;
            return OtpRequestResult.MustWait(Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds)));
        }

        var code = codes.GenerateFor(phone);
        var expiresAt = clock.UtcNow + OtpChallenge.Lifetime;

        await challenges.CreateAsync(phone, OtpPurpose.SignIn, Hash(code), expiresAt, customerId: null, cancellationToken);
        await challenges.SaveChangesAsync(cancellationToken);

        // The verification channel, not the campaign one: a sign-in code has to
        // reach a number that blocked advertising SMS, and only the provider's
        // service line does that — see ISmsSender. The wording lives in the
        // template registered with the provider, so only the code is passed.
        await sms.SendVerificationAsync(phone, code, cancellationToken);

        return OtpRequestResult.Success;
    }

    public async Task<(OtpVerifyResult? Result, OtpVerifyFailure? Failure)> VerifyOtpAsync(
        string phone,
        string code,
        CancellationToken cancellationToken)
    {
        var challenge = await challenges.FindActiveAsync(phone, OtpPurpose.SignIn, cancellationToken);
        if (challenge is null)
        {
            return (null, OtpVerifyFailure.NoActiveChallenge);
        }

        var outcome = challenge.Validate(Hash(code), clock.UtcNow);
        await challenges.SaveChangesAsync(cancellationToken);

        if (outcome != OtpChallenge.Outcome.Accepted)
        {
            var failure = outcome switch
            {
                OtpChallenge.Outcome.Expired or OtpChallenge.Outcome.AlreadyUsed => OtpVerifyFailure.Expired,
                OtpChallenge.Outcome.TooManyAttempts => OtpVerifyFailure.TooManyAttempts,
                _ => OtpVerifyFailure.WrongCode,
            };
            return (null, failure);
        }

        // Read-then-insert used to be two steps here, and two verifications of
        // the same code for the same new number arriving together both saw
        // nothing and both inserted. The repository owns that race now, so this
        // is one call and `isNewUser` is whichever of them actually created the
        // account rather than whichever asked first.
        var (customer, isNewUser) = await customers.GetOrCreateByPhoneAsync(phone, cancellationToken);

        // Checked after the code is validated rather than before it, so the
        // answer does not turn a sign-in form into a way to ask which numbers
        // belong to suspended accounts: you have to hold the code for that
        // number to learn anything, by which point you are the account holder.
        //
        // `IsBlocked` was read in four places — the panel's filter, its status
        // column, the customer statistics and the notification fan-out — and
        // enforced in none. A suspended customer signed in exactly as before,
        // so the flag described a state the system did not actually have.
        if (customer.IsBlocked)
        {
            return (null, OtpVerifyFailure.Blocked);
        }

        var token = tokens.GenerateCustomerToken(customer.Id, customer.Phone, customer.SecurityStamp);
        var result = new OtpVerifyResult(
            customer.Id,
            string.IsNullOrEmpty(customer.FirstName) ? null : customer.FirstName,
            string.IsNullOrEmpty(customer.LastName) ? null : customer.LastName,
            isNewUser,
            token,
            customer.SecurityStamp);

        return (result, null);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }
}
