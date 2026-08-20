using System.Security.Cryptography;
using System.Text;
using Bojan.Application.Common;
using Bojan.Application.Notifications;
using Bojan.Domain.Identity;

namespace Bojan.Application.Auth;

public enum EmailVerificationRequestFailure
{
    /// <summary>The account has no email on file to send a link to.</summary>
    NoEmail,
}

public sealed record EmailVerificationRequestResult(
    bool Sent, int RetryAfterSeconds = 0, EmailVerificationRequestFailure? Failure = null)
{
    public static readonly EmailVerificationRequestResult Success = new(true);

    public static EmailVerificationRequestResult MustWait(int seconds) => new(false, seconds);

    public static readonly EmailVerificationRequestResult NoEmailOnFile =
        new(false, Failure: EmailVerificationRequestFailure.NoEmail);
}

/// <summary>
/// Proves a customer's email address by mailing them a one-time link — the
/// email equivalent of <see cref="PhoneVerificationService"/>, modelled on
/// <see cref="CustomerPasswordService"/>'s password-reset link for the same
/// reason: a link, not a code, because there is nowhere on this screen to type
/// six digits back in.
/// </summary>
public sealed class EmailVerificationService(
    ICustomerRepository customers,
    IEmailVerificationTokenStore tokens,
    ICustomerMailer mailer,
    EmailTemplates templates,
    EmailLinks links,
    IDateTimeProvider clock)
{
    /// <summary>
    /// How long a verification link is good for. Longer than a password reset
    /// — nothing about this link grants access to anything, so there is no
    /// reason to rush it.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public async Task<EmailVerificationRequestResult> RequestAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await customers.FindByIdAsync(customerId, cancellationToken);
        if (customer?.Email is not { Length: > 0 } email)
        {
            return EmailVerificationRequestResult.NoEmailOnFile;
        }

        var now = clock.UtcNow;
        var pending = await tokens.FindActiveForCustomerAsync(customerId, now, cancellationToken);
        if (pending is not null)
        {
            var remaining = pending.ExpiresAtUtc - now;
            return EmailVerificationRequestResult.MustWait(Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds)));
        }

        var raw = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

        tokens.Add(new EmailVerificationToken
        {
            CustomerId = customerId,
            Email = email,
            TokenHash = Hash(raw),
            ExpiresAtUtc = now + Lifetime,
            CreatedAtUtc = now,
        });

        await customers.SaveChangesAsync(cancellationToken);

        await mailer.SendAsync(email, templates.EmailVerification(links.VerifyEmail(raw), Lifetime), cancellationToken);

        return EmailVerificationRequestResult.Success;
    }

    /// <summary>
    /// Spends the link. One outcome for unknown, expired, already-used and
    /// stale-address tokens — none of them is the caller's business to tell
    /// apart, and a token whose email no longer matches the account (the
    /// customer changed it again after the link was sent) must not verify the
    /// new value.
    /// </summary>
    public async Task<bool> ConfirmAsync(string rawToken, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var token = await tokens.FindActiveAsync(Hash(rawToken.Trim()), now, cancellationToken);
        if (token is null || !token.Consume(now))
        {
            return false;
        }

        var customer = await customers.FindByIdAsync(token.CustomerId, cancellationToken);
        if (customer is null || !string.Equals(customer.Email, token.Email, StringComparison.Ordinal))
        {
            await customers.SaveChangesAsync(cancellationToken);
            return false;
        }

        customer.IsEmailVerified = true;
        await customers.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
