using System.Security.Cryptography;
using System.Text;
using Bojan.Application.Common;
using Bojan.Application.Notifications;
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
    string Phone,
    /// <summary>Rotated by a password reset, so a session signed before one stops authenticating.</summary>
    Guid SecurityStamp);

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
    ICustomerMailer mailer,
    EmailTemplates templates,
    EmailLinks links,
    IJwtTokenGenerator tokens,
    IAdminUserRepository operators,
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

        // This answer does tell the caller the number is known, and there is no
        // wording that avoids it: a registration form either creates the account
        // or explains why it did not, and "created" for an account that already
        // belongs to someone else is worse than the disclosure. The frontend
        // collapses this and the address conflict below into one message so it
        // at least does not say *which*, and the endpoint's own rate limit is
        // what makes asking repeatedly impractical. Removing the oracle rather
        // than pricing it means verifying the phone before the account is
        // created — a change to the sign-up screens, not to this check.
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

        // After the save, so a customer is never welcomed to an account that
        // failed to be created. The mailer swallows its own failures, so this
        // cannot turn a successful registration into an error.
        await mailer.SendAsync(address, templates.Welcome(customer.FirstName), cancellationToken);

        return new CustomerAuthResult(
            customer.Id, null, null, IsNewUser: true, Issue(customer), customer.Phone, customer.SecurityStamp);
    }

    // --- signing in ----------------------------------------------------------

    /// <summary>
    /// <c>POST /auth/login</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One failure for every reason — unknown identity, an account that has no
    /// password, a wrong password. Distinguishing them turns this into a way to
    /// ask which phone numbers and addresses the shop knows.
    /// </para>
    /// <para>
    /// The same message is not on its own the same answer. Returning early for
    /// an identity with no password to check left the reply measurably faster
    /// than one that ran a 210,000-iteration verification, which is the same
    /// disclosure by a different channel. Both paths now do the hashing work;
    /// see <see cref="IPasswordHasher.PlaceholderHash"/>.
    /// </para>
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

        // Verified against a placeholder where there is nothing to verify
        // against, so the two outcomes cost the same. The result is discarded
        // because it is always false — the point is the time it took.
        if (customer?.PasswordHash is null || !passwords.Verify(request.Password, customer.PasswordHash))
        {
            // No customer here, or the wrong password for one. Before refusing,
            // the same credentials are offered to the operator table — see
            // SignInAsOperatorAsync for why an operator is also a shopper.
            if (await SignInAsOperatorAsync(identity, request.Password, cancellationToken) is { } asOperator)
            {
                return asOperator;
            }

            // Verified against a placeholder where there is nothing to verify
            // against, so the two outcomes cost the same. The result is
            // discarded because it is always false — the point is the time.
            if (customer?.PasswordHash is null)
            {
                passwords.Verify(request.Password, passwords.PlaceholderHash);
            }

            return UseCaseResult<CustomerAuthResult>.Failure(UseCaseError.Unauthorized, "invalid-credentials");
        }

        // After the password is checked, for the same reason the OTP path
        // checks after the code: answering before it would let anybody discover
        // which addresses belong to suspended accounts without holding the
        // credential. Its own key, because "your account is suspended" and "that
        // password is wrong" send a person to two different places.
        if (customer.IsBlocked)
        {
            return UseCaseResult<CustomerAuthResult>.Failure(UseCaseError.Forbidden, "account-blocked");
        }

        return new CustomerAuthResult(
            customer.Id,
            Blank(customer.FirstName),
            Blank(customer.LastName),
            IsNewUser: false,
            Issue(customer),
            customer.Phone,
            customer.SecurityStamp);
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

        // A suspended account is treated exactly like an unknown address: no
        // token, no mail, and the same silence to the caller. Sending the link
        // would be offering a way back in to the one person an operator has
        // decided to keep out, and refusing *differently* would turn this into
        // the membership oracle the silence exists to prevent.
        if (customer is null || customer.IsBlocked)
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

        // The link is assembled here now. It used to send the bare token as
        // the whole body — the customer received a string of hex with nothing
        // to do with it, and no way to reach the page that would spend it.
        await mailer.SendAsync(
            address,
            templates.PasswordReset(links.ResetPassword(raw), ResetLifetime),
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

        // A link issued before the suspension is still a live token, and
        // spending it would set a password on an account that may not use one.
        // The token has already been consumed above, so this also spends it —
        // which is right: it is not a link that should keep working.
        if (customer.IsBlocked)
        {
            await customers.SaveChangesAsync(cancellationToken);
            return UseCaseResult.Failure(UseCaseError.Forbidden, "account-blocked");
        }

        customer.PasswordHash = passwords.Hash(request.NewPassword);

        // Every session open on the old password ends here. Resetting is what
        // someone does when they believe another person is in their account,
        // and a reset that leaves that person signed in has not done the thing
        // it was reached for.
        customer.RotateSecurityStamp();

        // Every other outstanding link dies with this one — a second mail in
        // the same inbox must not still open the account that was just secured.
        await resetTokens.InvalidateAllAsync(customer.Id, now, cancellationToken);
        await customers.SaveChangesAsync(cancellationToken);

        // The only signal the account's owner gets that someone changed their
        // password — and the explanation for why every device just signed out.
        await mailer.SendAsync(customer.Email, templates.PasswordChanged(now), cancellationToken);

        return UseCaseResult.Success();
    }

    // --- helpers -------------------------------------------------------------

    /// <summary>
    /// The same credentials, offered to the operator table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An operator is a person, and people who run a shop buy from it. Their
    /// credentials opened the panel and nothing else, so the same person needed
    /// a second account with a second password to place an order — and the shop
    /// could not tell that the two were the same person.
    /// </para>
    /// <para>
    /// One password, kept in one place. This verifies against the operator
    /// record and then signs the caller in as their linked customer, so there
    /// is no second hash to keep in step and no moment where the two could
    /// disagree about what the password is.
    /// </para>
    /// <para>
    /// The shopping account is created on the first storefront sign-in rather
    /// than when the operator is appointed: most operators never shop, and a
    /// row nobody asked for is one that only ever puzzles whoever reads the
    /// customer list.
    /// </para>
    /// <para>
    /// Null when this is not an operator, when the password is wrong, or when
    /// the account is suspended — every one of which the caller turns into the
    /// same refusal a wrong customer password gets, because saying which would
    /// be a way to ask the shop who its operators are.
    /// </para>
    /// </remarks>
    private async Task<UseCaseResult<CustomerAuthResult>?> SignInAsOperatorAsync(
        string identity,
        string password,
        CancellationToken cancellationToken)
    {
        var operatorAccount = await operators.FindByIdentityAsync(identity, cancellationToken);

        if (operatorAccount is null
            || !operatorAccount.IsActive
            || !passwords.Verify(password, operatorAccount.PasswordHash))
        {
            return null;
        }

        if (operatorAccount.CustomerId is { } linkedId
            && await customers.FindByIdAsync(linkedId, cancellationToken) is { } linked)
        {
            return linked.IsBlocked
                ? UseCaseResult<CustomerAuthResult>.Failure(UseCaseError.Forbidden, "account-blocked")
                : Session(linked, isNewUser: false);
        }

        // A customer is a phone number — it is the unique sign-in key and the
        // column is required. An operator who has not given one has nothing to
        // build a shopping account from, and the honest answer names the field
        // rather than refusing as though the password were wrong.
        if (string.IsNullOrWhiteSpace(operatorAccount.Phone))
        {
            return UseCaseResult<CustomerAuthResult>.Failure(UseCaseError.Invalid, "operator-needs-phone");
        }

        // GetOrCreate rather than a read and an insert: the operator may
        // already shop here under that number, in which case the two accounts
        // are the same person and this links them instead of colliding on the
        // unique index.
        var (customer, created) = await customers.GetOrCreateByPhoneAsync(
            operatorAccount.Phone, cancellationToken);

        if (created)
        {
            // Only on a row this call created. Writing over an existing
            // customer's own name and address because an operator happens to
            // share their number would be editing somebody else's account.
            var space = operatorAccount.Name.IndexOf(' ', StringComparison.Ordinal);
            customer.FirstName = space > 0 ? operatorAccount.Name[..space] : operatorAccount.Name;
            customer.LastName = space > 0 ? operatorAccount.Name[(space + 1)..] : string.Empty;

            if (!await customers.EmailTakenAsync(operatorAccount.Email, null, cancellationToken))
            {
                customer.Email = operatorAccount.Email;
            }
        }

        if (customer.IsBlocked)
        {
            return UseCaseResult<CustomerAuthResult>.Failure(UseCaseError.Forbidden, "account-blocked");
        }

        operatorAccount.CustomerId = customer.Id;
        await customers.SaveChangesAsync(cancellationToken);
        await operators.SaveChangesAsync(cancellationToken);

        return Session(customer, created);
    }

    private UseCaseResult<CustomerAuthResult> Session(Customer customer, bool isNewUser) =>
        new CustomerAuthResult(
            customer.Id,
            Blank(customer.FirstName),
            Blank(customer.LastName),
            isNewUser,
            Issue(customer),
            customer.Phone,
            customer.SecurityStamp);

    private string Issue(Customer customer) =>
        tokens.GenerateCustomerToken(customer.Id, customer.Phone, customer.SecurityStamp);

    /// <summary>Lower-cased and trimmed, so one address is one account however it was typed.</summary>
    private static string Normalise(string email) => email.Trim().ToLowerInvariant();

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
