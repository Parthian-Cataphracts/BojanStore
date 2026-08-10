using Bojan.Application.Common;
using Bojan.Application.Contracts;

namespace Bojan.Application.Payments;

/// <summary>
/// The panel's payment settings screen, owner only.
/// </summary>
/// <remarks>
/// Two stores behind one screen because they are one decision to the person
/// making it: which gateway takes the money, and which ways of paying the
/// checkout offers. They are separate underneath because the second is
/// <c>PaymentMethod</c> rows the checkout already reads, and folding it into
/// the settings table would recreate the switch that switched nothing.
/// </remarks>
public sealed class PaymentSettingsService(
    IPaymentGatewaySettingsStore gatewaySettings,
    IPaymentMethodSwitchStore methodSwitches,
    IPaymentGatewayProbe probe,
    IAuditLog audit)
{
    public async Task<PaymentSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        new(
            await gatewaySettings.GetAsync(cancellationToken),
            await methodSwitches.GetAsync(cancellationToken));

    /// <summary>
    /// Saves the screen.
    /// </summary>
    /// <remarks>
    /// The callback address is validated here rather than left to the gateway,
    /// because a relative or malformed one is a payment that starts and never
    /// comes back — and the operator finds out from a customer rather than from
    /// this form. An unknown provider name is refused outright rather than
    /// stored and quietly read back as "none".
    /// </remarks>
    public async Task<UseCaseResult> SaveAsync(SavePaymentSettingsRequest request, CancellationToken cancellationToken)
    {
        if (!PaymentProviders.IsKnown(request.Provider))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "provider");
        }

        var callback = request.CallbackUrl.Trim();

        if (request.Provider is not PaymentProviders.None && !IsAbsoluteHttpUrl(callback))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "callbackUrl");
        }

        // A provider that needs a credential cannot be turned on without one.
        // Stored half-configured, it looks selected on the screen and fails on
        // the first order.
        if (request.Provider is PaymentProviders.ZarinPal)
        {
            var current = await gatewaySettings.GetAsync(cancellationToken);
            var supplied = request.MerchantId?.Trim() ?? string.Empty;

            if (supplied.Length == 0 && !current.HasMerchantId)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "merchantId");
            }
        }

        await gatewaySettings.SaveAsync(
            new PaymentGatewaySettingsDto(
                request.Provider,
                request.UseSandboxEndpoints,
                // Not read on the way in — the store keeps whatever is already
                // there when no new value is supplied.
                HasMerchantId: false,
                callback,
                request.Description.Trim()),
            request.MerchantId,
            cancellationToken);

        await methodSwitches.SaveAsync(request.Methods, cancellationToken);

        // The merchant id never appears in the entry. What is worth being able
        // to reconstruct later is who pointed the shop at which gateway and
        // when, not the credential they used to do it.
        audit.Record("payment.settings.saved", request.Provider);

        return UseCaseResult.Success();
    }

    /// <summary>Does this configuration actually work — the screen's test button.</summary>
    public Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken) =>
        probe.TestAsync(cancellationToken);

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";
}
