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
    IAuditLog audit,
    IPaymentDefaults defaults,
    IUnitOfWork unitOfWork)
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

        // Every string on this request is non-nullable in the record and
        // optional on the wire, which is not the same thing: a body that omits
        // one deserialises to null and the first `.Trim()` threw a 500 the
        // operator saw as "ذخیره اطلاعات انجام نشد" with nothing in it to act on.
        var callback = request.CallbackUrl?.Trim() ?? string.Empty;
        var description = request.Description?.Trim() ?? string.Empty;
        var methods = request.Methods ?? new PaymentMethodSwitchesDto(true, true, true);

        /*
            The sandbox needs no address from the operator.

            It is the built-in stub, and it returns the shopper to the storefront
            this API was configured with — `Payment:ReturnUrl`, which the compose
            file already points at the real storefront. Demanding one anyway is
            how «درگاه آزمایشی» came to look broken: the field starts empty,
            there is nothing an operator could sensibly type in it for a gateway
            that contacts no bank, and pressing save returned a refusal naming a
            field the screen had given them no reason to fill.

            A real gateway still has to have one. There the address is registered
            on the terminal and validated by the provider, so guessing it is
            worse than refusing.
        */
        if (callback.Length == 0 && request.Provider is PaymentProviders.Sandbox)
        {
            callback = defaults.ReturnUrl;
        }

        if (request.Provider is not PaymentProviders.None && !IsAbsoluteHttpUrl(callback))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "callbackUrl");
        }

        // A provider that needs a credential cannot be turned on without one.
        // Stored half-configured, it looks selected on the screen and fails on
        // the first order.
        if (PaymentProviders.NeedsCredential(request.Provider)
            // Zibal publishes a shared test merchant, so its sandbox needs no
            // credential of the shop's own — see ZibalPaymentGateway.
            && !(request.Provider is PaymentProviders.Zibal && request.UseSandboxEndpoints))
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
                description),
            request.MerchantId,
            cancellationToken);

        await methodSwitches.SaveAsync(methods, cancellationToken);

        // The merchant id never appears in the entry. What is worth being able
        // to reconstruct later is who pointed the shop at which gateway and
        // when, not the credential they used to do it.
        audit.Record("payment.settings.saved", request.Provider);

        // Committed here, not left to a caller. The stores above save
        // themselves, so an entry added after them had nothing to write it: the
        // panel's «تاریخچه فعالیت» stayed empty no matter how many settings were
        // changed, while every catalogue write showed up. See IAuditLog.Record —
        // it adds to the change tracker and deliberately does not save.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult.Success();
    }

    /// <summary>Does this configuration actually work — the screen's test button.</summary>
    public Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken) =>
        probe.TestAsync(cancellationToken);

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";
}
