namespace Bojan.Application.Auth;

/// <summary>Exact shape of <c>apps/admin/.../admin-auth/login/route.ts</c>'s <c>LoginResponse</c>.</summary>
public sealed record AdminLoginResult(Guid Id, string Name, string Email, string Role, bool RequiresTwoFactor, string Token);

/// <summary>
/// Panel sign-in: identity (phone or email) + password.
/// </summary>
/// <remarks>
/// One message for every rejection reason — wrong password, unknown account,
/// inactive account — so the response never confirms which accounts exist.
/// The frontend already enforces this same rule for its own mock path; this
/// is the real one underneath it.
/// </remarks>
public sealed class AdminAuthService(IAdminUserRepository admins, IPasswordHasher hasher, IJwtTokenGenerator tokens)
{
    public async Task<AdminLoginResult?> LoginAsync(string identity, string password, CancellationToken cancellationToken)
    {
        var admin = await admins.FindByIdentityAsync(identity, cancellationToken);
        if (admin is null || !admin.IsActive)
        {
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
