using System.Security.Cryptography;
using Bojan.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bojan.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>Where the gateway sends the shopper. The sandbox points this at the storefront's own callback.</summary>
    public string ReturnUrl { get; set; } = "http://localhost:3000/checkout/payment/callback";

    /// <summary>Base URL of the real gateway. Empty means the sandbox is in use.</summary>
    public string? GatewayUrl { get; set; }
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
/// that must not survive into production. Registering this class is gated on
/// <see cref="PaymentOptions.GatewayUrl"/> being unset, and it logs a warning
/// every time it is used, so a deployment that quietly kept the sandbox is
/// visible in the logs rather than only in the accounts.
/// </para>
/// </remarks>
public sealed class SandboxPaymentGateway(IOptions<PaymentOptions> options, ILogger<SandboxPaymentGateway> logger)
    : IPaymentGateway
{
    private readonly PaymentOptions _options = options.Value;

    public Task<PaymentSession> StartAsync(string orderNumber, long amountToman, CancellationToken cancellationToken)
    {
        var reference = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();

        logger.LogWarning(
            "Sandbox payment gateway issued reference {Reference} for order {OrderNumber} ({Amount} Toman). No real payment was taken.",
            reference,
            orderNumber,
            amountToman);

        var url = $"{_options.ReturnUrl}?order={Uri.EscapeDataString(orderNumber)}&ref={reference}";
        return Task.FromResult(new PaymentSession(url, reference));
    }

    public Task<bool> VerifyAsync(string reference, long amountToman, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Sandbox payment gateway approved reference {Reference} for {Amount} Toman without contacting a bank.",
            reference,
            amountToman);

        return Task.FromResult(true);
    }
}
