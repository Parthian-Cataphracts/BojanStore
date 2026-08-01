using Bojan.Domain.Common;

namespace Bojan.Domain.Identity;

/// <summary>
/// A pending SMS verification code.
/// </summary>
/// <remarks>
/// <para>
/// The frontend used to carry this entirely client-side, as a signed,
/// http-only cookie holding a hashed code and an attempt count (see
/// <c>apps/storefront/src/lib/auth/session.ts</c>, <c>OtpChallenge</c>). Now
/// that a real backend exists, the challenge belongs here instead — durable
/// across API instances, not tied to one browser's cookie jar.
/// </para>
/// <para>
/// The code itself is never stored, only its hash — matching the frontend's
/// own reasoning: a stolen challenge row must not reveal the code. Five
/// minutes and five attempts mirror <c>OTP_MAX_AGE</c> /
/// <c>OTP_MAX_ATTEMPTS</c> in that same file, so the two sides of this feature
/// agree even though the enforcement moved.
/// </para>
/// </remarks>
public sealed class OtpChallenge : Entity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    public const int MaxAttempts = 5;

    public required string Phone { get; init; }

    /// <summary>Hex SHA-256 of the code — never the code itself.</summary>
    public required string CodeHash { get; init; }

    public int Attempts { get; private set; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public bool Consumed { get; private set; }

    public enum Outcome
    {
        Accepted,
        WrongCode,
        Expired,
        TooManyAttempts,
        AlreadyUsed,
    }

    /// <summary>
    /// Checks a submitted code's hash against this challenge, consuming it on
    /// success. A wrong guess costs an attempt; the fifth burns the challenge
    /// entirely, matching the frontend's original behaviour.
    /// </summary>
    public Outcome Validate(string submittedCodeHash, DateTimeOffset nowUtc)
    {
        if (Consumed)
        {
            return Outcome.AlreadyUsed;
        }

        if (nowUtc >= ExpiresAtUtc)
        {
            return Outcome.Expired;
        }

        if (Attempts >= MaxAttempts)
        {
            return Outcome.TooManyAttempts;
        }

        if (!string.Equals(CodeHash, submittedCodeHash, StringComparison.Ordinal))
        {
            Attempts++;
            return Attempts >= MaxAttempts ? Outcome.TooManyAttempts : Outcome.WrongCode;
        }

        Consumed = true;
        return Outcome.Accepted;
    }
}
