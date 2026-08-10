using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Payments;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Infrastructure.Payments;

/// <summary>
/// The gateway the shop is currently pointed at, decided per call.
/// </summary>
/// <remarks>
/// <para>
/// Registration used to make this choice, which meant the choice was made at
/// startup from configuration and could only be changed by a deploy. The owner
/// enters a merchant id in the panel, and it has to work on the next order —
/// so the branch moved here, where every call re-reads which provider is
/// selected.
/// </para>
/// <para>
/// <see cref="PaymentProviders.None"/> is the state a shop starts in and it is
/// deliberately not an error: a shop with no gateway yet still sells, on cash
/// on delivery and wallet balance, and the checkout already knows how to place
/// an order that has nothing to redirect to. What it must never do is silently
/// behave like a gateway that took money — so asking this to start a payment
/// while no provider is chosen throws, and the checkout turns that into
/// <c>payment-unavailable</c> against a real order number.
/// </para>
/// </remarks>
public sealed class ConfiguredPaymentGateway(
    IServiceScopeFactory scopeFactory,
    ZarinPalPaymentGateway zarinPal,
    SandboxPaymentGateway sandbox) : IPaymentGateway, IPaymentGatewayProbe
{
    public async Task<PaymentSession> StartAsync(
        string orderNumber,
        long amountToman,
        CancellationToken cancellationToken)
    {
        var provider = await ReadProviderAsync(cancellationToken);

        return provider switch
        {
            PaymentProviders.ZarinPal => await zarinPal.StartAsync(orderNumber, amountToman, cancellationToken),
            PaymentProviders.Sandbox => await sandbox.StartAsync(orderNumber, amountToman, cancellationToken),
            _ => throw new InvalidOperationException(
                "No payment gateway is configured. Choose one under تنظیمات ← پرداخت."),
        };
    }

    public async Task<bool> VerifyAsync(string reference, long amountToman, CancellationToken cancellationToken)
    {
        var provider = await ReadProviderAsync(cancellationToken);

        return provider switch
        {
            PaymentProviders.ZarinPal => await zarinPal.VerifyAsync(reference, amountToman, cancellationToken),
            PaymentProviders.Sandbox => await sandbox.VerifyAsync(reference, amountToman, cancellationToken),
            // Not false. False is "the bank says this was not paid", which is
            // written to an order as a failed payment; this is "nobody was
            // asked", and settling on it would record an answer that was never
            // given.
            _ => throw new InvalidOperationException("No payment gateway is configured to verify against."),
        };
    }

    /// <inheritdoc />
    public async Task<bool> TakesRealMoneyAsync(CancellationToken cancellationToken) =>
        await ReadProviderAsync(cancellationToken) is PaymentProviders.ZarinPal;

    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken)
    {
        var provider = await ReadProviderAsync(cancellationToken);

        return provider switch
        {
            PaymentProviders.ZarinPal => await zarinPal.TestAsync(cancellationToken),
            PaymentProviders.Sandbox => ProviderTestResult.Success(
                "درگاه آزمایشی داخلی فعال است — هیچ پرداختی واقعی نیست."),
            _ => ProviderTestResult.Fail("هنوز درگاهی انتخاب نشده است."),
        };
    }

    /// <remarks>
    /// Through a scope because the store is scoped over the database context
    /// while this is a singleton, and singletons on the money path are what
    /// keep a checkout from paying for a container per request.
    /// </remarks>
    private async Task<string> ReadProviderAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<PaymentGatewaySettingsStore>();
        var settings = await store.GetAsync(cancellationToken);

        // A provider chosen but left without a credential is the same as no
        // provider: it cannot start a payment, and reporting it as configured
        // would let wallet top-up believe a bank is in the loop.
        return settings.Provider is PaymentProviders.ZarinPal && !settings.HasMerchantId
            ? PaymentProviders.None
            : settings.Provider;
    }
}
