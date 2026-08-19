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
/// own reasoning: a stolen challenge row must not reveal the code. Two
/// minutes and five attempts mirror <c>OTP_MAX_AGE</c> /
/// <c>OTP_MAX_ATTEMPTS</c> in that same file, so the two sides of this feature
/// agree even though the enforcement moved.
/// </para>
/// <para>
/// The same two minutes is also the floor between one request and the next
/// for a phone — see <see cref="AuthService.RequestOtpAsync"/>. A live
/// challenge blocks a fresh one rather than being silently replaced by it, so
/// a shopper cannot get a new SMS every few seconds by resubmitting the phone
/// step, and reloading the page cannot get around that: the wait is a
/// property of this row, not of anything held in the browser.
/// </para>
/// </remarks>
public sealed class OtpChallenge : Entity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);
    public const int MaxAttempts = 5;

    public required string Phone { get; init; }

    /// <summary>Hex SHA-256 of the code — never the code itself.</summary>
    public required string CodeHash { get; init; }

    public int Attempts { get; private set; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public bool Consumed { get; private set; }

    /// <summary>
    /// Whether this row is a live code still worth waiting for — the reason a
    /// fresh request for the same phone should be refused rather than served.
    /// </summary>
    /// <remarks>
    /// A challenge that has been consumed is done regardless of its expiry —
    /// the shopper who used it may reasonably want to sign in again a moment
    /// later — so this is not simply "not yet expired". A challenge that ran
    /// out its five attempts is still counted as blocking: it has not expired
    /// either, and a wrong guess exhausting the count and then asking again
    /// straight away would turn the attempt limit into a way to get a new
    /// code faster than a shopper who guessed right the first time.
    /// </remarks>
    public bool BlocksResend(DateTimeOffset nowUtc) => !Consumed && nowUtc < ExpiresAtUtc;

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
