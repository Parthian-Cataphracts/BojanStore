using Bojan.Application.Auth;
using Bojan.Infrastructure.Auth;
using Bojan.Infrastructure.Support;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Bojan.Infrastructure.Notifications;

/// <summary>
/// Sends outbound mail over SMTP, using the same account the support mailbox
/// already reads.
/// </summary>
/// <remarks>
/// <para>
/// One account, configured once. The shop already stores a full IMAP/SMTP
/// account for the support inbox — host, port, TLS, username, an encrypted
/// password, the address to send from and the name to send it under — and every
/// one of those is exactly what an outbound message needs. Asking an operator
/// to enter the same credentials a second time under a different screen would
/// be two settings to keep in step, and the one that drifts is the one nobody
/// looks at until an order confirmation stops arriving.
/// </para>
/// <para>
/// Falls back to the console sender rather than throwing when the mailbox is
/// switched off or half-configured. A shop that has not set mail up yet still
/// has to be able to take an order, and the fourteen transactional templates
/// are all raised on paths — placing an order, resetting a password — where
/// failing the request over a mail server would be the wrong trade. The console
/// sender says loudly that the message went nowhere, which is the right amount
/// of noise for a deployment that has not finished being configured.
/// </para>
/// <para>
/// Nothing here retries. A transient SMTP failure loses one message, and the
/// alternative — holding the request open through a backoff — is worse on every
/// path this is called from. A durable outbox is the real answer if these ever
/// have to be guaranteed, and it is a queue and a worker, not a loop here.
/// </para>
/// </remarks>
public sealed class SmtpEmailSender(
    IServiceScopeFactory scopeFactory,
    ConsoleEmailSender fallback,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken,
        string? html = null)
    {
        // Read per message rather than captured at startup: the settings screen
        // can change the account while the process is running, and a sender
        // holding the old host would keep using it until the next deploy.
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<MailboxSettingsStore>();

        var (mailbox, password) = await store.GetWithPasswordAsync(cancellationToken);

        // The same four conditions the health check reports on, asked in one
        // place so the board and the sender cannot come to disagree about
        // whether this shop can send mail.
        var readiness = new MailboxSendReadiness(
            mailbox.Enabled,
            mailbox.SmtpHost.Length > 0,
            mailbox.Address.Length > 0,
            password.Length > 0);

        if (!readiness.IsReady)
        {
            await fallback.SendAsync(email, subject, body, cancellationToken, html);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(mailbox.DisplayName, mailbox.Address));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;

        // Both parts, always. The text alternative is what a text-only client
        // shows, what a screen reader reads most reliably, and what survives a
        // spam filter that strips the markup — see IEmailSender.
        var builder = new BodyBuilder { TextBody = body };
        if (html is { Length: > 0 }) builder.HtmlBody = html;
        message.Body = builder.ToMessageBody();

        try
        {
            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                mailbox.SmtpHost,
                mailbox.SmtpPort,
                mailbox.SmtpUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable,
                cancellationToken);

            await smtp.AuthenticateAsync(mailbox.Username, password, cancellationToken);
            await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(quit: true, cancellationToken);

            logger.LogInformation("Sent \"{Subject}\" to {Email}.", subject, email);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Logged, not thrown. Every caller reaches this after the work it
            // describes has already been committed — the order is placed, the
            // password is reset — so throwing would fail a request that has
            // succeeded, and the customer would be told their order did not go
            // through while the shop believes it did.
            logger.LogError(
                exception, "Could not send \"{Subject}\" to {Email} over SMTP.", subject, email);
        }
    }
}
