using System.Security.Cryptography;
using System.Text;
using Bojan.Application.Common;
using Bojan.Domain.Customers;
using Bojan.Domain.Identity;

namespace Bojan.Application.Auth;

/// <summary>What a successful register or password sign-in hands back — the same shape the OTP path returns.</summary>
public sealed record CustomerAuthResult(
    Guid CustomerId,
    string? FirstName,
    string? LastName,
    bool IsNewUser,
    string Token,
    /// <summary>The account phone. Signing in by email still has to produce a session that knows it.</summary>
    string Phone);

public sealed record RegisterRequest(string Phone, string Email, string Password);

/// <summary><c>Identity</c> is a phone number or an email — the form does not make the customer say which.</summary>
public sealed record PasswordLoginRequest(string Identity, string Password);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>
/// Password sign-in for shoppers, beside the one-time code rather than instead
/// of it.
/// </summary>
/// <remarks>
/// <para>
/// The reason this exists is delivery, not preference: SMS to Iranian networks
/// fails often enough that a shop whose only door is a text message loses sales
/// it never hears about. A password is a second door. <see cref="AuthService"/>
/// still owns the first one and is untouched.
/// </para>
/// <para>
/// Kept separate from that class rather than added to it because the two share
/// nothing but the customer record: no challenge row, no code generator, no SMS
/// port. Merging them would put four unrelated dependencies on one service so
/// that each half could ignore the other's.
/// </para>
/// </remarks>
public sealed class CustomerPasswordService(
    ICustomerRepository customers,
    IPasswordResetTokenStore resetTokens,
    IPasswordHasher passwords,
    IEmailSender email,
    IJwtTokenGenerator tokens,
    IDateTimeProvider clock)
{
    /// <summary>How long a reset link is good for.</summary>
    /// <remarks>Long enough to walk to a different device for the mail, short enough that a forwarded link is stale.</remarks>
    private static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);

    // --- registering ---------------------------------------------------------

    public async Task<UseCaseResult<CustomerAuthResult>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var phone = request.Phone.Trim();
        var address = Normalise(request.Email);

        if (PasswordPolicy.Validate(request.Password) is { } weak)
        {
            return UseCaseResult<CustomerAuthResult>.Failure(UseCaseError.Invalid, weak);
        }

        // An existing account is not an error to shout about: the phone is the
        // sign-in key, so someone registering a number they already have is
        // someone who forgot. Telling them "that number is taken" also confirms
        // the number is registered to anyone who asks.
        var existing = await customers.FindByPhoneAsync(phone, cancellationToken);
        if (existing is not null)
        {
            return UseCaseResult<CustomerAuthResult>.Failure(UseCaseError.Conflict, "already-registered");
        }

        if (await customers.EmailTakenAsync(address, exceptCustomerId: null, cancellationToken))
        {
            return UseCaseResult<CustomerAuthResult>.Failure(UseCaseError.Conflict, "email-taken");
        }

        var customer = new Customer
        {
            Phone = phone,
            Email = address,
            PasswordHash = passwords.Hash(request.Password),
        };

        await customers.AddAsync(customer, cancellationToken);
        await customers.SaveChangesAsync(cancellationToken);

        return new CustomerAuthResult(customer.Id, null, null, IsNewUser: true, Issue(customer), customer.Phone);
    }

    // --- signing in ----------------------------------------------------------

    /// <summary>
    /// <c>POST /auth/login</c>.
    /// </summary>
    /// <remarks>
    /// One failure for every reason — unknown identity, an account that has no
    /// password, a wrong password. Distinguishing them turns this into a way to
    /// ask which phone numbers and addresses the shop knows.
    /// </remarks>
    public async Task<UseCaseResult<CustomerAuthResult>> LoginAsync(
        PasswordLoginRequest request,
        CancellationToken cancellationToken)
    {
        var identity = request.Identity.Trim();

        // Bounded before hashing, not after: Verify runs the full PBKDF2
        // iteration count, so an unbounded password is a free way to spend this
        // server's CPU without holding an account.
        if (request.Password.Length is 0 or > PasswordPolicy.MaxLength)
        {
            return UseCaseResult<CustomerAuthResult>.Failure(UseCaseError.Unauthorized, "invalid-credentials");
        }

        var customer = identity.Contains('@', StringComparison.Ordinal)
            ? await customers.FindByEmailAsync(Normalise(identity), cancellationToken)
            : await customers.FindByPhoneAsync(identity, cancellationToken);

        if (customer?.PasswordHash is null || !passwords.Verify(request.Password, customer.PasswordHash))
        {
            return UseCaseResult<CustomerAuthResult>.Failure(UseCaseError.Unauthorized, "invalid-credentials");
        }

        return new CustomerAuthResult(
            customer.Id,
            Blank(customer.FirstName),
            Blank(customer.LastName),
            IsNewUser: false,
            Issue(customer),
            customer.Phone);
    }

    // --- forgetting and resetting -------------------------------------------

    /// <summary>
    /// <c>POST /auth/forgot-password</c>. Always succeeds from the caller's
    /// point of view.
    /// </summary>
    /// <remarks>
    /// A different answer for a known address than for an unknown one turns
    /// this endpoint into a membership oracle for every email someone cares to
    /// try. The mail is sent only when there is somewhere to send it; the
    /// response does not say which happened.
    /// </remarks>
    public async Task RequestResetAsync(string emailAddress, CancellationToken cancellationToken)
    {
        var address = Normalise(emailAddress);
        var customer = await customers.FindByEmailAsync(address, cancellationToken);

        if (customer is null)
        {
            return;
        }

        // The raw token is generated here, emailed, and never stored — only its
        // hash goes in the row, so this table is worth nothing if it leaks.
        var raw = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

        resetTokens.Add(new PasswordResetToken
        {
            CustomerId = customer.Id,
            TokenHash = Hash(raw),
            ExpiresAtUtc = clock.UtcNow + ResetLifetime,
            CreatedAtUtc = clock.UtcNow,
        });

        await customers.SaveChangesAsync(cancellationToken);

        // The link itself is assembled by the caller that knows the site's
        // address; this port only carries the token.
        await email.SendAsync(
            address,
            "بازیابی رمز عبور بوژان",
            raw,
            cancellationToken);
    }

    public async Task<UseCaseResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (PasswordPolicy.Validate(request.NewPassword) is { } weak)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, weak);
        }

        var now = clock.UtcNow;
        var token = await resetTokens.FindActiveAsync(Hash(request.Token.Trim()), now, cancellationToken);

        // Expired, already used and never-existed are one answer. Which of the
        // three it was is not the caller's business, and saying so would let
        // someone probe for live tokens.
        if (token is null || !token.Consume(now))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "invalid-token");
        }

        var customer = await customers.FindByIdAsync(token.CustomerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "invalid-token");
        }

        customer.PasswordHash = passwords.Hash(request.NewPassword);

        // Every other outstanding link dies with this one — a second mail in
        // the same inbox must not still open the account that was just secured.
        await resetTokens.InvalidateAllAsync(customer.Id, now, cancellationToken);
        await customers.SaveChangesAsync(cancellationToken);

        return UseCaseResult.Success();
    }

    // --- helpers -------------------------------------------------------------

    private string Issue(Customer customer) => tokens.GenerateCustomerToken(customer.Id, customer.Phone);

    /// <summary>Lower-cased and trimmed, so one address is one account however it was typed.</summary>
    private static string Normalise(string email) => email.Trim().ToLowerInvariant();

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
