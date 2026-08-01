using Bojan.Application.Auth;
using Microsoft.Extensions.Logging;

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
/// </remarks>
public sealed class ConsoleSmsSender(ILogger<ConsoleSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phone, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("[SMS -> {Phone}] {Message}", phone, message);
        return Task.CompletedTask;
    }
}
