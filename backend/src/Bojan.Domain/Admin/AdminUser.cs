using Bojan.Domain.Common;

namespace Bojan.Domain.Admin;

/// <summary>
/// A panel operator.
/// </summary>
/// <remarks>
/// Sign-in is identity (phone or email) + password, unlike the storefront's
/// phone + SMS code — see <c>apps/admin/src/app/api/admin-auth/login/route.ts</c>,
/// which already calls <c>POST /auth/login</c> with
/// <c>{ identity, password }</c> and expects
/// <c>{ id, name, email, role, requiresTwoFactor? }</c> back.
/// </remarks>
public sealed class AdminUser : Entity
{
    public required string Name { get; set; }

    public required string Email { get; set; }

    /// <summary>Optional — an operator may sign in with either this or <see cref="Email"/>.</summary>
    public string? Phone { get; set; }

    public required string PasswordHash { get; set; }

    public required AdminRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The operator has a password somebody else chose, and must replace it
    /// before the panel is of any use to them.
    /// </summary>
    /// <remarks>
    /// An account created from screen 145 starts with a password its owner
    /// typed and then read out over a desk or a phone line — so between the
    /// account existing and the operator's first sign-in, the credential is
    /// known to at least two people, and the one who does not own it has no
    /// reason to stop knowing it. The flag is what makes that window close on
    /// its own rather than on the new operator remembering to close it.
    ///
    /// Set by the owner's create and by the owner's password reset, and cleared
    /// only by <c>POST /me/password</c> — the one route where the operator
    /// proves they know the current password before choosing the next.
    /// </remarks>
    public bool MustChangePassword { get; set; }

    public bool TwoFactorEnabled { get; set; }

    /// <summary>TOTP secret, present only once two-factor is set up (screen 153).</summary>
    public string? TwoFactorSecret { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    /// <summary>
    /// Changes whenever every existing session for this operator must stop
    /// working — the same device <c>Customer.SecurityStamp</c> plays on the
    /// storefront.
    /// </summary>
    /// <remarks>
    /// The panel's cookie is signed and self-contained and lasts a working day,
    /// so without this an operator who changed their password because it had
    /// been seen over their shoulder left the watcher's session open for the
    /// rest of that day. Role and <see cref="IsActive"/> were already read from
    /// this table on every request; the password was the one change nothing
    /// could reach.
    /// </remarks>
    public Guid SecurityStamp { get; private set; } = Guid.NewGuid();

    public void RotateSecurityStamp() => SecurityStamp = Guid.NewGuid();
}
