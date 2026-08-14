namespace Bojan.Application.Payments;

/// <summary>
/// The addresses this API was configured with, for the one gateway that has
/// nobody to ask.
/// </summary>
/// <remarks>
/// The sandbox contacts no bank and has no terminal to register a callback
/// with, so the operator has nothing to type into that field — see
/// <c>PaymentSettingsService.SaveAsync</c>, where demanding one anyway was what
/// made «درگاه آزمایشی» impossible to turn on. A port rather than a direct read
/// of <c>PaymentOptions</c> because that type lives in Infrastructure, on the
/// other side of this layer's dependency arrow.
/// </remarks>
public interface IPaymentDefaults
{
    /// <summary>Where a payment returns the shopper when the panel has not said.</summary>
    string ReturnUrl { get; }
}
