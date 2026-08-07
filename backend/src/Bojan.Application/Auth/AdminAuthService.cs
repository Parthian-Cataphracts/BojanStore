using Bojan.Application.Common;
using Bojan.Domain.Identity;

namespace Bojan.Application.Auth;

/// <summary>Exact shape of <c>apps/admin/.../admin-auth/login/route.ts</c>'s <c>LoginResponse</c>.</summary>
/// <remarks>
/// <see cref="Token"/> and <see cref="Challenge"/> are mutually exclusive: a
/// sign-in either completed, and carries the session token, or stopped at the
/// second factor, and carries the challenge that step consumes. No answer has
/// both.
/// </remarks>
public sealed record AdminLoginResult(
    Guid Id,
    string Name,
    string Email,
    string Role,
    bool RequiresTwoFactor,
    string? Token,
    string? Challenge,
    /// <summary>
    /// Null alongside <see cref="Challenge"/>: the sign-in has not finished, so
    /// there is no session to stamp yet.
    /// </summary>
    Guid? SecurityStamp);

/// <summary>
/// Panel sign-in: identity (phone or email) + password, then a TOTP code when
/// the account has one.
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
/// <para>
/// An account with two-factor enabled gets no session token from
/// <see cref="LoginAsync"/>, only a challenge. That is what the factor is for:
/// a password alone must not be enough, and returning the token beside
/// <c>requiresTwoFactor</c> — which is what this did — meant it was.
/// </para>
/// </remarks>
public sealed class AdminAuthService(
    IAdminUserRepository admins,
    IPasswordHasher hasher,
    IJwtTokenGenerator tokens,
    IDateTimeProvider clock)
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

        // A secret that was never confirmed cannot produce a code anyone can
        // enter, so an account flagged for two-factor without one would be
        // locked out rather than protected.
        if (admin.TwoFactorEnabled && !string.IsNullOrWhiteSpace(admin.TwoFactorSecret))
        {
            return new AdminLoginResult(
                admin.Id,
                admin.Name,
                admin.Email,
                RoleToWireFormat(admin.Role),
                RequiresTwoFactor: true,
                Token: null,
                Challenge: tokens.GenerateTwoFactorChallenge(admin.Id),
                SecurityStamp: null);
        }

        return await CompleteAsync(admin, cancellationToken);
    }

    /// <summary>
    /// The second half of a two-factor sign-in: the challenge
    /// <see cref="LoginAsync"/> issued, plus a code from the operator's
    /// authenticator.
    /// </summary>
    /// <remarks>
    /// The password is not re-sent, and does not need to be — the challenge is
    /// proof it was accepted, it expires in minutes, and it names exactly one
    /// operator.
    /// </remarks>
    public async Task<AdminLoginResult?> VerifyTwoFactorAsync(
        string challenge,
        string code,
        CancellationToken cancellationToken)
    {
        if (tokens.ReadTwoFactorChallenge(challenge) is not { } adminId)
        {
            return null;
        }

        var admin = await admins.FindByIdAsync(adminId, cancellationToken);
        if (admin is null || !admin.IsActive || string.IsNullOrWhiteSpace(admin.TwoFactorSecret))
        {
            return null;
        }

        if (!Totp.Verify(admin.TwoFactorSecret, code, clock.UtcNow))
        {
            return null;
        }

        return await CompleteAsync(admin, cancellationToken);
    }

    /// <summary>Issues the session token and stamps the sign-in screen 145 lists.</summary>
    private async Task<AdminLoginResult> CompleteAsync(
        Domain.Admin.AdminUser admin,
        CancellationToken cancellationToken)
    {
        admin.LastLoginAtUtc = clock.UtcNow;
        await admins.SaveChangesAsync(cancellationToken);

        return new AdminLoginResult(
            admin.Id,
            admin.Name,
            admin.Email,
            RoleToWireFormat(admin.Role),
            RequiresTwoFactor: false,
            Token: tokens.GenerateAdminToken(admin.Id, admin.Role, admin.SecurityStamp),
            Challenge: null,
            SecurityStamp: admin.SecurityStamp);
    }

    /// <summary>
    /// The exact lowercase strings <c>apps/admin/src/lib/auth/session.ts</c>'s
    /// <c>AdminRole</c> type expects — <c>'owner' | 'product' | 'sales' | 'support'</c>.
    /// </summary>
    private static string RoleToWireFormat(Domain.Admin.AdminRole role) => role.ToString().ToLowerInvariant();
}
