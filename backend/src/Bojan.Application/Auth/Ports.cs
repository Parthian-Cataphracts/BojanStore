using Bojan.Domain.Admin;
using Bojan.Domain.Customers;

namespace Bojan.Application.Auth;

/// <summary>
/// Everything <see cref="AuthService"/> and <see cref="AdminAuthService"/> need
/// from the outside world, named from the use case's point of view. Bojan.Infrastructure
/// implements every one of these; Bojan.Application never references EF Core,
/// Npgsql or an SMS provider directly.
/// </summary>

public interface ICustomerRepository
{
    Task<Customer?> FindByPhoneAsync(string phone, CancellationToken cancellationToken);

    Task<Customer> AddAsync(Customer customer, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IAdminUserRepository
{
    /// <summary>Looks up by whichever the operator typed — screen 91 accepts phone or email.</summary>
    Task<AdminUser?> FindByIdentityAsync(string identity, CancellationToken cancellationToken);

    /// <summary>The operator a two-factor challenge names. Only the challenge ever supplies this id.</summary>
    Task<AdminUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Sends the actual SMS. The only implementation in Phase 1 logs instead of sending — see <c>BACKEND.md</c>'s note that a gateway is a later decision.</summary>
public interface ISmsSender
{
    Task SendAsync(string phone, string message, CancellationToken cancellationToken);
}

/// <summary>
/// Durable storage for pending OTP challenges — the server-side replacement
/// for the frontend's signed cookie (see <see cref="Domain.Identity.OtpChallenge"/>
/// for why the challenge moved here).
/// </summary>
public interface IOtpChallengeStore
{
    /// <summary>Replaces any existing challenge for this phone — a new request always supersedes the old one.</summary>
    Task<Domain.Identity.OtpChallenge> CreateAsync(string phone, string codeHash, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken);

    Task<Domain.Identity.OtpChallenge?> FindActiveAsync(string phone, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>PBKDF2, not the frontend's SHA-256 — that was for a challenge cookie's integrity, this is for a stored password.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

/// <summary>
/// Produces the code an OTP challenge is built from.
/// </summary>
/// <remarks>
/// A port rather than a private method on <see cref="AuthService"/> so that
/// local development can substitute a fixed code for one known number without
/// touching the sign-in flow itself — the challenge row, the attempt counter,
/// the expiry and the verify path stay exactly as they are in production. See
/// <c>Bojan.Infrastructure.Auth.StaticOtpCodeGenerator</c> and the
/// Development-only registration that is the only thing which ever selects it.
/// </remarks>
public interface IOtpCodeGenerator
{
    string GenerateFor(string phone);
}

public interface IJwtTokenGenerator
{
    string GenerateCustomerToken(Guid customerId, string phone);

    string GenerateAdminToken(Guid adminId, AdminRole role);

    /// <summary>
    /// A short-lived token that names an operator who has passed the password
    /// step and nothing more.
    /// </summary>
    /// <remarks>
    /// It carries its own scope, so it authenticates no endpoint: the only
    /// thing that accepts it is <c>POST /admin/auth/2fa</c>, which exchanges it
    /// for a real session once a code verifies. Issuing the session token
    /// before the second factor would make the factor decorative.
    /// </remarks>
    string GenerateTwoFactorChallenge(Guid adminId);

    /// <summary>The operator a challenge names, or null when it is absent, forged, expired or of another scope.</summary>
    Guid? ReadTwoFactorChallenge(string? challenge);
}
