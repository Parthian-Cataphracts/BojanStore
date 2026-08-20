using Bojan.Domain.Common;

namespace Bojan.Domain.Identity;

/// <summary>
/// A pending "confirm this email address" link.
/// </summary>
/// <remarks>
/// Same shape as <see cref="PasswordResetToken"/>, and for the same reasons:
/// the row stores a hash of the token rather than the token itself, so a dump
/// of this table hands an attacker nothing to present back, and
/// <see cref="Consume"/> is the only way to spend one — single-use and
/// short-lived, both enforced rather than assumed.
/// </remarks>
public sealed class EmailVerificationToken : Entity
{
    public required Guid CustomerId { get; init; }

    /// <summary>
    /// The address this token proves. Checked again at confirm time — if the
    /// customer's email changed after the link was sent, the old link must not
    /// verify the new address.
    /// </summary>
    public required string Email { get; init; }

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
