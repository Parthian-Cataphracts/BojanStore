using System.Security.Cryptography;
using Bojan.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bojan.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>
    /// Where to send the shopper back to when the panel has not been told.
    /// </summary>
    /// <remarks>
    /// The address is a property of the storefront, not of this API, so the
    /// operator sets it on the payment settings screen. This default only
    /// covers a developer who has not opened that screen yet — a real gateway
    /// checks the callback against the domain registered on the terminal, so
    /// localhost is never what production is running on.
    /// </remarks>
    public string ReturnUrl { get; set; } = "http://localhost:3000/checkout/payment/callback";
}

/// <summary>
/// A payment gateway that approves everything.
/// </summary>
/// <remarks>
/// <para>
/// The same role <c>ConsoleSmsSender</c> plays for SMS: a real implementation of
/// the port with the outside world stubbed, so the money path can be built and
/// tested end to end before a PSP contract exists. It returns a
/// <c>paymentUrl</c>, which is all the checkout needs — it already redirects
/// whenever one is present.
/// </para>
/// <para>
/// <see cref="VerifyAsync"/> returning <c>true</c> unconditionally is the part
/// that must not survive into production. Three things keep it from doing so
/// quietly: it logs a warning every time it is used, so a deployment that kept
/// the sandbox shows up in the logs rather than only in the accounts; it is
/// reachable only while the panel's payment screen has this provider
/// explicitly selected, which is never the default; and
/// <see cref="TakesRealMoneyAsync"/> is false, which is what stops wallet
/// top-up from minting balance against it.
/// </para>
/// </remarks>
public sealed class SandboxPaymentGateway(
    IOptions<PaymentOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<SandboxPaymentGateway> logger) : IPaymentGateway
{
    private readonly PaymentOptions _options = options.Value;

    public Task<bool> TakesRealMoneyAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    public async Task<PaymentSession> StartAsync(
        string orderNumber,
        long amountToman,
        CancellationToken cancellationToken)
    {
        var reference = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();

        logger.LogWarning(
            "Sandbox payment gateway issued reference {Reference} for order {OrderNumber} ({Amount} Toman). No real payment was taken.",
            reference,
            orderNumber,
            amountToman);

        // The same query shape a real gateway returns with, so the storefront's
        // callback page has one path to maintain rather than one per provider.
        var callback = await ReadCallbackUrlAsync(cancellationToken);
        var separator = callback.Contains('?') ? '&' : '?';
        var url = $"{callback}{separator}Authority={Uri.EscapeDataString(reference)}&Status=OK";

        return new PaymentSession(url, reference);
    }

    /// <remarks>
    /// The operator's address when there is one, so switching between this and
    /// a real gateway does not also mean editing configuration. Through a scope
    /// because the store is scoped over the database context.
    /// </remarks>
    private async Task<string> ReadCallbackUrlAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<PaymentGatewaySettingsStore>();
        var settings = await store.GetAsync(cancellationToken);

        return settings.CallbackUrl is { Length: > 0 } stored ? stored : _options.ReturnUrl;
    }

    public Task<bool> VerifyAsync(
        string reference,
        string orderNumber,
        long amountToman,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Sandbox payment gateway approved reference {Reference} for {Amount} Toman without contacting a bank.",
            reference,
            amountToman);

        return Task.FromResult(true);
    }
}
