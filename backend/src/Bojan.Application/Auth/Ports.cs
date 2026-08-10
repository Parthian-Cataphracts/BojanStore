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

    /// <summary>
    /// The account for a number, creating it if this is the first time the
    /// number has signed in.
    /// </summary>
    /// <remarks>
    /// One method rather than a read followed by an insert, because between
    /// those two steps another request can create the same account: two
    /// verifications of the same code for the same new number arriving together
    /// both saw nothing, both inserted, and the unique index on Phone refused
    /// the second — which reached the caller as a failure to sign in to an
    /// account that by then existed and was theirs. Resolving that needs to know
    /// what the database refused and why, so it belongs on this side of the
    /// port.
    /// </remarks>
    /// <returns>The account, and whether this call is what created it.</returns>
    Task<(Customer Customer, bool Created)> GetOrCreateByPhoneAsync(
        string phone,
        CancellationToken cancellationToken);

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

    /// <summary>The operator a two-factor challenge names. Only the challenge ever supplies this id.</summary>
    Task<AdminUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Sends the actual SMS.
/// </summary>
/// <remarks>
/// <para>
/// Two methods rather than one, because Iranian providers genuinely have two
/// products behind one account and they are not interchangeable. A one-time
/// code goes out on a <i>service</i> line as a pre-approved template with the
/// code substituted in: it is delivered at high priority and it reaches numbers
/// that have opted out of advertising, which is most numbers. A campaign goes
/// out on the shop's own <i>advertising</i> line as free text, and a code sent
/// that way arrives late or not at all.
/// </para>
/// <para>
/// Collapsing them into one "send this string" call is what makes an
/// integration look finished and then fail for the customers who most need it —
/// a shopper who blocked advertising SMS years ago and now cannot sign in.
/// </para>
/// </remarks>
public interface ISmsSender
{
    /// <summary>
    /// Free text on the shop's own line — campaigns and operational notices.
    /// </summary>
    Task SendAsync(string phone, string message, CancellationToken cancellationToken);

    /// <summary>
    /// A one-time code, on the service line, through the provider's template.
    /// </summary>
    /// <param name="code">
    /// Substituted into the template the operator registered with the provider.
    /// Nothing else about the message is chosen here: the wording lives with
    /// the provider, because it is the wording they approved.
    /// </param>
    Task SendVerificationAsync(string phone, string code, CancellationToken cancellationToken);
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
    /// <summary>
    /// Sends one message.
    /// </summary>
    /// <param name="body">
    /// The plain-text body. Always sent, and always written to stand on its
    /// own: it is what a text-only client shows, what a screen reader reads
    /// most reliably, and what survives when a spam filter strips the HTML.
    /// </param>
    /// <param name="html">
    /// The formatted alternative, or null for a message that has none. A
    /// provider sends both as a multipart alternative and lets the client
    /// choose; the console stand-in logs the text.
    /// </param>
    Task SendAsync(
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken,
        string? html = null);
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
    /// <summary>
    /// Mints a customer token.
    /// </summary>
    /// <param name="securityStamp">
    /// Carried so the token can be refused once it is rotated — see
    /// <c>Customer.SecurityStamp</c>. Without it a token stays good for its
    /// whole lifetime whatever happens to the account behind it.
    /// </param>
    string GenerateCustomerToken(Guid customerId, string phone, Guid securityStamp);

    /// <summary>
    /// Mints an operator token.
    /// </summary>
    /// <param name="securityStamp">
    /// Carried for the same reason the customer token carries one — see
    /// <c>AdminUser.SecurityStamp</c>.
    /// </param>
    string GenerateAdminToken(Guid adminId, AdminRole role, Guid securityStamp);

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

/// <summary>
/// Names for the security stamp as it travels on a session.
/// </summary>
/// <remarks>
/// Here rather than beside the check that reads them because the token
/// generator writes the claim and the API reads it, and those live in different
/// projects — a literal in each is how the two quietly stop agreeing.
/// </remarks>
public static class CustomerSessionClaims
{
    /// <summary>The stamp, on a JWT and on the principal either scheme produces.</summary>
    public const string SecurityStamp = "stamp";

    /// <summary>The header the storefront's proxy sends beside <c>X-Customer-Id</c>.</summary>
    public const string StampHeader = "X-Customer-Stamp";
}

/// <summary>
/// The same two names for an operator's session.
/// </summary>
/// <remarks>
/// The claim is deliberately the identical string: a principal only ever
/// carries one <c>scope</c>, so one stamp claim cannot be mistaken for the
/// other, and the code that reads it stays a single line. The header has to
/// differ because the two proxies are different servers sending different
/// identities.
/// </remarks>
public static class AdminSessionClaims
{
    public const string SecurityStamp = CustomerSessionClaims.SecurityStamp;

    /// <summary>The header the panel's proxy sends beside <c>X-Admin-User</c>.</summary>
    public const string StampHeader = "X-Admin-Stamp";
}
