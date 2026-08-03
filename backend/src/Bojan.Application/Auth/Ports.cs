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

    /// <summary>Case-insensitive. Used by password sign-in and by the reset request.</summary>
    Task<Customer?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<Customer?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Whether any other account already uses this email.
    /// </summary>
    /// <remarks>
    /// An email has to identify one account or it cannot be a sign-in
    /// identifier, and a reset sent to a shared address would be ambiguous
    /// about whose password it changes.
    /// </remarks>
    Task<bool> EmailTakenAsync(string email, Guid? exceptCustomerId, CancellationToken cancellationToken);

    Task<Customer> AddAsync(Customer customer, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IAdminUserRepository
{
    /// <summary>Looks up by whichever the operator typed — screen 91 accepts phone or email.</summary>
    Task<AdminUser?> FindByIdentityAsync(string identity, CancellationToken cancellationToken);

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

/// <summary>
/// Sends transactional email — today only the password-reset link.
/// </summary>
/// <remarks>
/// A separate port from <see cref="ISmsSender"/> rather than a channel argument
/// on it, because the reason this exists is that the two channels fail
/// independently: SMS delivery is the thing password sign-in is a way around,
/// so the reset path must not depend on it. Same shape as the SMS port, and the
/// only implementation logs rather than sends until a provider is chosen.
/// </remarks>
public interface IEmailSender
{
    Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken);
}

/// <summary>Durable storage for pending password resets — see <see cref="Domain.Identity.PasswordResetToken"/>.</summary>
public interface IPasswordResetTokenStore
{
    void Add(Domain.Identity.PasswordResetToken token);

    /// <summary>
    /// Finds an unspent token by its hash. Returns null for one that is
    /// unknown, expired or already used.
    /// </summary>
    /// <remarks>
    /// <paramref name="now"/> is passed in rather than read from the clock
    /// here: the store has no business deciding what "now" is when the service
    /// already holds an <c>IDateTimeProvider</c>, and a <c>DateTimeOffset.UtcNow</c>
    /// written inside the query expression is not translatable anyway.
    /// </remarks>
    Task<Domain.Identity.PasswordResetToken?> FindActiveAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates every outstanding token for a customer.
    /// </summary>
    /// <remarks>
    /// Called when a reset succeeds. Without it, a second link sitting in the
    /// same inbox — or one an attacker triggered earlier — would still work
    /// against the password that was just set.
    /// </remarks>
    Task InvalidateAllAsync(Guid customerId, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>PBKDF2, not the frontend's SHA-256 — that was for a challenge cookie's integrity, this is for a stored password.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);

    /// <summary>
    /// A real hash of nothing, for verifying against when there is no account.
    /// </summary>
    /// <remarks>
    /// Sign-in returns one message whichever way it fails, but the work it does
    /// getting there is not the same: an unknown identity is answered without
    /// hashing anything, while a known one pays for a full PBKDF2 verification
    /// first. That difference is measurable from outside, and it turns a login
    /// form into a way to ask which phone numbers and addresses the shop has on
    /// file. Verifying the supplied password against this instead costs the same
    /// as verifying it against a real one.
    /// </remarks>
    string PlaceholderHash { get; }
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
}
