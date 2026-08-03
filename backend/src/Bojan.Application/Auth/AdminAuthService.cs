using Bojan.Domain.Identity;

namespace Bojan.Application.Auth;

/// <summary>Exact shape of <c>apps/admin/.../admin-auth/login/route.ts</c>'s <c>LoginResponse</c>.</summary>
public sealed record AdminLoginResult(Guid Id, string Name, string Email, string Role, bool RequiresTwoFactor, string Token);

/// <summary>
/// Panel sign-in: identity (phone or email) + password.
/// </summary>
/// <remarks>
/// <para>
/// One message for every rejection reason — wrong password, unknown account,
/// inactive account — so the response never confirms which accounts exist.
/// The frontend already enforces this same rule for its own mock path; this
/// is the real one underneath it.
/// </para>
/// <para>
/// Answering in the same time is part of answering the same thing. An unknown
/// or deactivated operator used to be rejected without hashing anything, while
/// a real one cost a full PBKDF2 verification first — a difference an attacker
/// can measure, and the panel's account list is a more valuable thing to
/// enumerate than the storefront's. Both paths do the work now.
/// </para>
/// </remarks>
public sealed class AdminAuthService(IAdminUserRepository admins, IPasswordHasher hasher, IJwtTokenGenerator tokens)
{
    public async Task<AdminLoginResult?> LoginAsync(string identity, string password, CancellationToken cancellationToken)
    {
        // Bounded before any hashing, as on the storefront's password door: the
        // iteration count is paid on whatever arrives, so an unbounded field is
        // a way to spend this server's CPU without holding an account.
        if (password.Length is 0 or > PasswordPolicy.MaxLength)
        {
            return null;
        }

        var admin = await admins.FindByIdentityAsync(identity, cancellationToken);

        if (admin is null || !admin.IsActive)
        {
            hasher.Verify(password, hasher.PlaceholderHash);
            return null;
        }

        if (!hasher.Verify(password, admin.PasswordHash))
        {
            return null;
        }

        var token = tokens.GenerateAdminToken(admin.Id, admin.Role);

        return new AdminLoginResult(
            admin.Id,
            admin.Name,
            admin.Email,
            RoleToWireFormat(admin.Role),
            admin.TwoFactorEnabled,
            token);
    }

    /// <summary>
    /// The exact lowercase strings <c>apps/admin/src/lib/auth/session.ts</c>'s
    /// <c>AdminRole</c> type expects — <c>'owner' | 'product' | 'sales' | 'support'</c>.
    /// </summary>
    private static string RoleToWireFormat(Domain.Admin.AdminRole role) => role.ToString().ToLowerInvariant();
}
