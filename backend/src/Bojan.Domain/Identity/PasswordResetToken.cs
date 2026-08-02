using Bojan.Domain.Common;

namespace Bojan.Domain.Identity;

/// <summary>
/// A pending "I forgot my password" request.
/// </summary>
/// <remarks>
/// <para>
/// The row stores a <em>hash</em> of the token, never the token itself — the
/// same reason <see cref="OtpChallenge"/> stores a hashed code. What reaches
/// the customer's inbox is the only copy of the secret; a dump of this table
/// hands an attacker nothing they can present back.
/// </para>
/// <para>
/// Single-use and short-lived, and both are enforced rather than assumed:
/// <see cref="Consume"/> is the only way to spend one, and it refuses a token
/// that is expired or already spent. A reset link forwarded, logged by a mail
/// gateway, or left in a browser's history is therefore worth nothing an hour
/// later or a second time.
/// </para>
/// </remarks>
public sealed class PasswordResetToken : Entity
{
    public required Guid CustomerId { get; init; }

    /// <summary>SHA-256 of the token that was emailed, hex-encoded.</summary>
    public required string TokenHash { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool IsSpent(DateTimeOffset now) => ConsumedAtUtc is not null || now >= ExpiresAtUtc;

    /// <summary>
    /// Marks the token spent. Returns false when it was already spent or has
    /// expired, which the caller must treat exactly like an unknown token.
    /// </summary>
    public bool Consume(DateTimeOffset now)
    {
        if (IsSpent(now))
        {
            return false;
        }

        ConsumedAtUtc = now;
        return true;
    }
}
