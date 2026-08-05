using Bojan.Application.Auth;
using Bojan.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Notifications;

/// <summary>
/// The swallow-and-skip mailer described by <see cref="ICustomerMailer"/>.
/// </summary>
/// <remarks>
/// In Infrastructure rather than beside the templates because it is the piece
/// that needs a logger, and the application project deliberately carries no
/// package dependencies — the same reason <see cref="INotificationDispatcher"/>
/// is a port with its implementation here.
/// </remarks>
public sealed class CustomerMailer(IEmailSender email, ILogger<CustomerMailer> logger) : ICustomerMailer
{
    public async Task SendAsync(
        string? address,
        (string Subject, EmailBody Body) message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        try
        {
            await email.SendAsync(address, message.Subject, message.Body.Text, cancellationToken, message.Body.Html);
        }
        catch (Exception exception)
        {
            // Warning rather than error: the event itself succeeded and the
            // customer has the in-app notification. What was lost is a copy.
            logger.LogWarning(exception, "Customer email could not be sent: {Subject}", message.Subject);
        }
    }
}
