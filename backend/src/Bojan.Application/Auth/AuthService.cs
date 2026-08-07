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

public enum OtpVerifyFailure
{
    NoActiveChallenge,
    Expired,
    WrongCode,
    TooManyAttempts,
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
    public async Task RequestOtpAsync(string phone, CancellationToken cancellationToken)
    {
        var code = codes.GenerateFor(phone);
        var expiresAt = clock.UtcNow + OtpChallenge.Lifetime;

        await challenges.CreateAsync(phone, Hash(code), expiresAt, cancellationToken);
        await challenges.SaveChangesAsync(cancellationToken);

        // A local/dev implementation of ISmsSender logs this instead of
        // sending it — see Bojan.Infrastructure's ConsoleSmsSender.
        await sms.SendAsync(phone, $"کد تایید بوژان: {code}", cancellationToken);
    }

    public async Task<(OtpVerifyResult? Result, OtpVerifyFailure? Failure)> VerifyOtpAsync(
        string phone,
        string code,
        CancellationToken cancellationToken)
    {
        var challenge = await challenges.FindActiveAsync(phone, cancellationToken);
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
