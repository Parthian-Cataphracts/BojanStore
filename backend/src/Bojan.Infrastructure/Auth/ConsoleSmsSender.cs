using Bojan.Application.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bojan.Infrastructure.Auth;

/// <summary>
/// Logs the SMS instead of sending it.
/// </summary>
/// <remarks>
/// The local/dev stand-in for a real gateway — the same role
/// <c>apps/storefront/src/app/api/auth/otp/request/route.ts</c> fills by
/// printing the mock code to the console. Swap the DI registration in
/// <c>DependencyInjection.cs</c> for a real provider once one is chosen; every
/// caller goes through <see cref="ISmsSender"/>, so nothing else changes.
///
/// The message body is printed only where <see cref="ConsoleSenderOptions"/>
/// allows it. It is the sign-in code: a log that carries it is a log that signs
/// anyone in, which is why a host that has not said it is a developer's gets
/// the recipient and nothing else.
/// </remarks>
public sealed class ConsoleSmsSender(
    IOptions<ConsoleSenderOptions> options,
    ILogger<ConsoleSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phone, string message, CancellationToken cancellationToken)
    {
        if (options.Value.LogMessageBodies)
        {
            logger.LogInformation("[SMS -> {Phone}] {Message}", phone, message);
        }
        else
        {
            logger.LogWarning(
                "No SMS provider is configured; a message to {Phone} was dropped rather than sent. "
                + "Its contents are deliberately not logged.",
                phone);
        }

        return Task.CompletedTask;
    }
}
