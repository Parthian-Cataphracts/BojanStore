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
    public Task SendAsync(
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken,
        string? html = null)
    {
        // The text body, not the HTML: a wall of table markup in a log line is
        // unreadable, and the text alternative says the same thing. Whether an
        // HTML part existed is worth knowing, so it is reported as a flag.
        logger.LogWarning(
            "No email provider is configured; the message below was logged rather than sent. "
            + "[EMAIL -> {Email}] {Subject}: {Body} (html: {HasHtml})",
            email,
            subject,
            body,
            html is { Length: > 0 });

        return Task.CompletedTask;
    }
}
