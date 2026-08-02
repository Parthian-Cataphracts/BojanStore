using Bojan.Application.Auth;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Auth;

/// <summary>
/// Logs the email instead of sending it.
/// </summary>
/// <remarks>
/// The same stand-in <see cref="ConsoleSmsSender"/> is for SMS, and it carries
/// the same warning: a password-reset token printed to a log is a token
/// anything reading that log can spend. Swap the registration in
/// <c>DependencyInjection.cs</c> for a real provider before this runs anywhere
/// a stranger can see the output — the warning below is so that a deployment
/// which forgot shows up in the logs rather than only in an incident.
/// </remarks>
public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "No email provider is configured; the message below was logged rather than sent. [EMAIL -> {Email}] {Subject}: {Body}",
            email,
            subject,
            body);

        return Task.CompletedTask;
    }
}
