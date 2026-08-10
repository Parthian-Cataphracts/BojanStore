using Bojan.Application.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bojan.Infrastructure.Auth;

/// <summary>
/// Logs the SMS instead of sending it.
/// </summary>
/// <remarks>
/// <para>
/// Where messages go while no provider is configured — the same role
/// <c>apps/storefront/src/app/api/auth/otp/request/route.ts</c> fills by
/// printing the mock code to the console. It is reached through
/// <c>ConfiguredSmsSender</c>, which picks it when the panel's SMS settings
/// name no provider or carry no key.
/// </para>
/// <para>
/// The message body is printed only where <see cref="ConsoleSenderOptions"/>
/// allows it. It is the sign-in code: a log that carries it is a log that signs
/// anyone in, which is why a host that has not said it is a developer's gets
/// the recipient and nothing else.
/// </para>
/// <para>
/// A dropped sign-in code is logged as an error rather than a warning. Everything
/// else this stands in for degrades — a campaign that does not go out is a
/// campaign — but a code that does not arrive is a customer who cannot sign in
/// at all, and that is worth being loud about in a shop that is taking orders.
/// </para>
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

    public Task SendVerificationAsync(string phone, string code, CancellationToken cancellationToken)
    {
        if (options.Value.LogMessageBodies)
        {
            logger.LogInformation("[SMS -> {Phone}] کد تایید بوژان: {Code}", phone, code);
        }
        else
        {
            logger.LogError(
                "No SMS provider is configured, so the sign-in code for {Phone} was dropped rather than sent — "
                + "that customer cannot sign in. Configure a provider under تنظیمات ← پیامک. "
                + "The code is deliberately not logged.",
                phone);
        }

        return Task.CompletedTask;
    }
}
