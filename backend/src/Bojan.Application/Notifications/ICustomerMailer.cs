namespace Bojan.Application.Notifications;

/// <summary>
/// Sends a customer-facing email, and never lets doing so break anything.
/// </summary>
/// <remarks>
/// <para>
/// Two rules the implementation must obey, and the reason this is a port rather
/// than <see cref="Auth.IEmailSender"/> being called from each use case.
/// </para>
/// <para>
/// <b>A missing address is a skip, not an error.</b> The shop's main sign-up
/// path is a phone number and an SMS code, so <c>Customer.Email</c> is genuinely
/// optional and many customers will not have one. Treating that as a failure
/// would make the normal case look broken.
/// </para>
/// <para>
/// <b>A failure never reaches the caller.</b> Placing an order moved money and
/// reserved stock; it must not fail because a mail server is down, and the
/// customer gets an in-app notification either way. So the implementation
/// swallows and logs, and every caller can treat this as fire-and-forget.
/// </para>
/// </remarks>
public interface ICustomerMailer
{
    /// <summary>
    /// Sends one built message.
    /// </summary>
    /// <param name="address">The customer's address, or null — null returns immediately.</param>
    Task SendAsync(
        string? address,
        (string Subject, EmailBody Body) message,
        CancellationToken cancellationToken);
}
