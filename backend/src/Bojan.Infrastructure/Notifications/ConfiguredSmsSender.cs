using Bojan.Application.Auth;
using Bojan.Application.Contracts;
using Bojan.Application.Notifications;
using Bojan.Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Infrastructure.Notifications;

/// <summary>
/// The SMS service the shop is currently pointed at, decided per message.
/// </summary>
/// <remarks>
/// <para>
/// The same arrangement <c>ConfiguredPaymentGateway</c> uses, for the same
/// reason: the account is entered in the panel, and a code requested a minute
/// later has to go out through it rather than after a deploy.
/// </para>
/// <para>
/// A shop with no provider chosen falls through to the console sender, which
/// says loudly that the message went nowhere and — outside Development —
/// deliberately does not print what it was. That is the honest behaviour for a
/// shop that has not been configured yet, and it is why this API no longer
/// refuses to start without SMS: the owner cannot reach the settings screen to
/// enter an account if the process that serves it will not boot.
/// </para>
/// </remarks>
public sealed class ConfiguredSmsSender(
    IServiceScopeFactory scopeFactory,
    SmsIrSender smsIr,
    ConsoleSmsSender fallback) : ISmsSender, ISmsProbe
{
    public async Task SendAsync(string phone, string message, CancellationToken cancellationToken)
    {
        if (await IsConfiguredAsync(cancellationToken))
        {
            await smsIr.SendAsync(phone, message, cancellationToken);
            return;
        }

        await fallback.SendAsync(phone, message, cancellationToken);
    }

    public async Task SendVerificationAsync(string phone, string code, CancellationToken cancellationToken)
    {
        if (await IsConfiguredAsync(cancellationToken))
        {
            await smsIr.SendVerificationAsync(phone, code, cancellationToken);
            return;
        }

        await fallback.SendVerificationAsync(phone, code, cancellationToken);
    }

    public async Task<ProviderTestResult> TestAsync(string phone, CancellationToken cancellationToken)
    {
        var settings = await ReadSettingsAsync(cancellationToken);

        return settings.Provider switch
        {
            SmsProviders.SmsIr => await smsIr.TestAsync(phone, cancellationToken),
            _ => ProviderTestResult.Fail("هنوز سرویس پیامکی انتخاب نشده است."),
        };
    }

    /// <remarks>
    /// A provider chosen but left without a key is the same as no provider:
    /// every message it was asked to send would be refused, and falling through
    /// to the console sender at least leaves a line in the log saying so.
    /// </remarks>
    private async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        return settings.Provider is SmsProviders.SmsIr && settings.HasApiKey;
    }

    private async Task<SmsSettingsDto> ReadSettingsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<SmsSettingsStore>();
        return await store.GetAsync(cancellationToken);
    }
}
