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
    /// <summary>How long a verification link is good for.</summary>
    /// <remarks>
    /// An hour. It was a day, on the reasoning that the link grants no access
    /// so there is no reason to rush — true in itself, and it ignored what a
    /// long-lived link costs elsewhere. A verification link is most often
    /// mistyped into somebody else's inbox, and every hour it stays alive is an
    /// hour that stranger can attach their address to this account. An hour is
    /// long enough to walk to a phone and read the mail.
    /// </remarks>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    /// <summary>How many links one account may ask for in <see cref="SendWindow"/>.</summary>
    /// <remarks>
    /// A quota rather than one-at-a-time. The old rule refused a new link while
    /// the previous one was unspent, so the customer who needed one most — the
    /// one who mistyped their address and got nothing — was told to wait out a
    /// link they would never receive. Five an hour is generous enough to cover
    /// a typo, a full mailbox and a slow mail server, and low enough that a
    /// signed-in account cannot be turned into a way to flood an inbox.
    /// </remarks>
    public const int MaxSendsPerWindow = 5;

    public static readonly TimeSpan SendWindow = TimeSpan.FromHours(1);

    public async Task<EmailVerificationRequestResult> RequestAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await customers.FindByIdAsync(customerId, cancellationToken);
        if (customer?.Email is not { Length: > 0 } email)
        {
            return EmailVerificationRequestResult.NoEmailOnFile;
        }

        var now = clock.UtcNow;

        var window = await tokens.CountSentSinceAsync(customerId, now - SendWindow, cancellationToken);
        if (window.Count >= MaxSendsPerWindow && window.OldestAtUtc is { } oldest)
        {
            /*
                Counted from the oldest send, not from now. The wait is what the
                caller puts on the button, and the old rule put the link's whole
                lifetime there — a customer who had mistyped their address
                watched a countdown measured in hours before they were allowed
                to correct it. What actually frees a slot is the earliest send
                ageing out of the window, which is usually minutes away.
            */
            var freesAt = oldest + SendWindow;
            return EmailVerificationRequestResult.MustWait(
                Math.Max(1, (int)Math.Ceiling((freesAt - now).TotalSeconds)));
        }

        // The previous link stops working the moment this one is sent. One
        // address per account is being proven, and leaving a link addressed to
        // a mistyped address alive is leaving a stranger able to spend it.
        await tokens.InvalidateForCustomerAsync(customerId, now, cancellationToken);

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
