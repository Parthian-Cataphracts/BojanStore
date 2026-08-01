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

    public bool TwoFactorEnabled { get; set; }

    /// <summary>TOTP secret, present only once two-factor is set up (screen 153).</summary>
    public string? TwoFactorSecret { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAtUtc { get; set; }
}
