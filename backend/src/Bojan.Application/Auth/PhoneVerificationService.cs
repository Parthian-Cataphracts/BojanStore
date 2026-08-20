using System.Security.Cryptography;
using System.Text;
using Bojan.Application.Common;
using Bojan.Domain.Identity;

namespace Bojan.Application.Auth;

/// <summary>Why <see cref="PhoneVerificationService.RequestAsync"/> refused, when it did.</summary>
public enum PhoneVerificationRequestFailure
{
    /// <summary>The candidate number already belongs to another account.</summary>
    PhoneTaken,
}

public sealed record PhoneVerificationRequestResult(
    bool Sent, int RetryAfterSeconds = 0, PhoneVerificationRequestFailure? Failure = null)
{
    public static readonly PhoneVerificationRequestResult Success = new(true);

    public static PhoneVerificationRequestResult MustWait(int seconds) => new(false, seconds);

    public static readonly PhoneVerificationRequestResult Taken = new(false, Failure: PhoneVerificationRequestFailure.PhoneTaken);
}

public enum PhoneVerificationConfirmFailure
{
    NoActiveChallenge,
    Expired,
    WrongCode,
    TooManyAttempts,

    /// <summary>
    /// The code was right, but between requesting it and confirming it someone
    /// else's account took the candidate number.
    /// </summary>
    PhoneTaken,
}

/// <summary>
/// Proves a customer holds a phone number — either the one already on their
/// account, or a new one they are asking to change to.
/// </summary>
/// <remarks>
/// <para>
/// One flow for both, because they differ only in which number the code goes
/// to. Verifying the current number just re-sends a code to
/// <c>Customer.Phone</c>; changing it sends one to the candidate and, on the
/// matching confirm, writes the candidate into <c>Customer.Phone</c> at that
/// moment — never before, so a number is never on the account until it has
/// been proven.
/// </para>
/// <para>
/// Reuses <see cref="OtpChallenge"/> rather than a second table, tagged
/// <see cref="OtpPurpose.PhoneVerification"/> so a pending login code and a
/// pending verification code for the same number do not collide — see the
/// unique index on <c>(Phone, Purpose)</c>.
/// </para>
/// </remarks>
public sealed class PhoneVerificationService(
    ICustomerRepository customers,
    IOtpChallengeStore challenges,
    ISmsSender sms,
    IOtpCodeGenerator codes,
    IDateTimeProvider clock)
{
    /// <summary>
    /// Sends a code proving <paramref name="targetPhone"/>, or the customer's
    /// own current phone when it is null.
    /// </summary>
    public async Task<PhoneVerificationRequestResult> RequestAsync(
        Guid customerId, string? targetPhone, CancellationToken cancellationToken)
    {
        var customer = await customers.FindByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return new PhoneVerificationRequestResult(false);
        }

        var phone = targetPhone ?? customer.Phone;

        // A candidate that is not the account's own number has to be free —
        // checked here as the honest early answer, and again at confirm time,
        // since the gap between the two is exactly where someone else could
        // claim it.
        if (!string.Equals(phone, customer.Phone, StringComparison.Ordinal))
        {
            var existing = await customers.FindByPhoneAsync(phone, cancellationToken);
            if (existing is not null)
            {
                return PhoneVerificationRequestResult.Taken;
            }
        }

        var pending = await challenges.FindActiveForCustomerAsync(
            customerId, OtpPurpose.PhoneVerification, cancellationToken);
        if (pending is not null && pending.BlocksResend(clock.UtcNow))
        {
            var remaining = pending.ExpiresAtUtc - clock.UtcNow;
            return PhoneVerificationRequestResult.MustWait(Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds)));
        }

        var code = codes.GenerateFor(phone);
        var expiresAt = clock.UtcNow + OtpChallenge.Lifetime;

        await challenges.CreateAsync(
            phone, OtpPurpose.PhoneVerification, Hash(code), expiresAt, customerId, cancellationToken);
        await challenges.SaveChangesAsync(cancellationToken);

        await sms.SendVerificationAsync(phone, code, cancellationToken);

        return PhoneVerificationRequestResult.Success;
    }

    /// <summary>
    /// Checks the code against whichever <see cref="OtpPurpose.PhoneVerification"/>
    /// challenge this customer is holding. On success, marks the phone
    /// verified — and, if the challenge named a number different from the
    /// account's current one, applies it as the new phone right here.
    /// </summary>
    public async Task<(bool Success, PhoneVerificationConfirmFailure? Failure)> ConfirmAsync(
        Guid customerId, string code, CancellationToken cancellationToken)
    {
        var challenge = await challenges.FindActiveForCustomerAsync(
            customerId, OtpPurpose.PhoneVerification, cancellationToken);
        if (challenge is null)
        {
            return (false, PhoneVerificationConfirmFailure.NoActiveChallenge);
        }

        var outcome = challenge.Validate(Hash(code), clock.UtcNow);
        await challenges.SaveChangesAsync(cancellationToken);

        if (outcome != OtpChallenge.Outcome.Accepted)
        {
            var failure = outcome switch
            {
                OtpChallenge.Outcome.Expired or OtpChallenge.Outcome.AlreadyUsed =>
                    PhoneVerificationConfirmFailure.Expired,
                OtpChallenge.Outcome.TooManyAttempts => PhoneVerificationConfirmFailure.TooManyAttempts,
                _ => PhoneVerificationConfirmFailure.WrongCode,
            };
            return (false, failure);
        }

        var customer = await customers.FindByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return (false, PhoneVerificationConfirmFailure.NoActiveChallenge);
        }

        if (!string.Equals(challenge.Phone, customer.Phone, StringComparison.Ordinal))
        {
            // Re-checked here, not trusted from the request step — the number
            // could have been claimed by someone else in the meantime.
            if (await customers.FindByPhoneAsync(challenge.Phone, cancellationToken) is not null)
            {
                return (false, PhoneVerificationConfirmFailure.PhoneTaken);
            }

            customer.Phone = challenge.Phone;
        }

        customer.IsPhoneVerified = true;
        await customers.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }
}
