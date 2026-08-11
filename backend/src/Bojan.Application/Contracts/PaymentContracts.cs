namespace Bojan.Application.Contracts;

/// <summary>
/// Which gateway the shop is pointed at.
/// </summary>
/// <remarks>
/// A stored value rather than a compile-time choice because the owner picks it
/// from the panel, and the shop has to keep taking orders while it is unset.
/// The wire values are lowercase and stable — they are written into the
/// settings table and read back by <c>ConfiguredPaymentGateway</c>.
/// </remarks>
public static class PaymentProviders
{
    /// <summary>No gateway. Online payment is simply not offered.</summary>
    public const string None = "none";

    /// <summary>The built-in stub that approves everything, for local work.</summary>
    public const string Sandbox = "sandbox";

    /// <summary>ZarinPal, over its v4 JSON API.</summary>
    public const string ZarinPal = "zarinpal";

    /// <summary>Zibal, over its v1 JSON API.</summary>
    public const string Zibal = "zibal";

    /// <summary>IDPay, over its v1.1 JSON API.</summary>
    public const string IdPay = "idpay";

    public static bool IsKnown(string value) =>
        value is None or Sandbox or ZarinPal or Zibal or IdPay;

    /// <summary>
    /// True for a provider that needs a credential before it can take money.
    /// </summary>
    /// <remarks>
    /// All three real ones authenticate with a single opaque string — ZarinPal's
    /// merchant id, Zibal's merchant, IDPay's API key — which is why one stored
    /// field serves them all. What the field is called on screen changes with
    /// the provider; what it is does not.
    /// </remarks>
    public static bool NeedsCredential(string value) =>
        value is ZarinPal or Zibal or IdPay;
}

/// <summary>
/// The gateway configuration, as the panel sees it.
/// </summary>
/// <remarks>
/// <para>
/// No merchant id. It is the credential that lets money be requested in this
/// shop's name, so it goes in and never comes back out — the same rule
/// <see cref="MailboxSettingsDto"/> follows for the mailbox password, and for
/// the same reason: a settings screen left open on a shared machine should not
/// be a credential on display. <see cref="HasMerchantId"/> is how the form
/// knows to show "ثبت شده" instead of an empty box.
/// </para>
/// <para>
/// <see cref="CallbackUrl"/> is the storefront address ZarinPal returns the
/// shopper to. It is stored rather than derived because the API has no way to
/// know the storefront's public address — and because ZarinPal validates it
/// against the domain registered on the terminal, so getting it wrong is an
/// error the operator has to be able to see and correct.
/// </para>
/// </remarks>
public sealed record PaymentGatewaySettingsDto(
    string Provider,
    bool UseSandboxEndpoints,
    bool HasMerchantId,
    string CallbackUrl,
    string Description);

/// <summary>
/// Which payment methods the checkout offers.
/// </summary>
/// <remarks>
/// These are <c>PaymentMethod</c> rows, not settings keys. The panel screen
/// used to write them into the settings table where nothing read them, which
/// meant switching cash on delivery off changed what the screen said and
/// nothing else. They are the same three codes the checkout validates against.
/// </remarks>
public sealed record PaymentMethodSwitchesDto(bool Online, bool Wallet, bool CashOnDelivery);

/// <summary>Everything the payment settings screen loads in one request.</summary>
public sealed record PaymentSettingsDto(PaymentGatewaySettingsDto Gateway, PaymentMethodSwitchesDto Methods);

/// <summary>What the payment settings screen submits.</summary>
/// <param name="MerchantId">
/// Null leaves the stored one alone, which is what an empty box on a form that
/// never shows the credential has to mean. An explicit empty string clears it.
/// </param>
public sealed record SavePaymentSettingsRequest(
    string Provider,
    bool UseSandboxEndpoints,
    string? MerchantId,
    string CallbackUrl,
    string Description,
    PaymentMethodSwitchesDto Methods);

/// <summary>
/// The outcome of asking a provider whether it is reachable and configured.
/// </summary>
/// <remarks>
/// Shaped like <see cref="MailResult"/> and for the same reason: every failure
/// belongs to the operator — a merchant id the terminal does not recognise, a
/// callback domain that does not match, an account still awaiting approval —
/// and each needs its own sentence on the screen rather than one 500 that says
/// nothing.
/// </remarks>
public sealed record ProviderTestResult(bool Ok, string? Error = null, string? Detail = null)
{
    public static ProviderTestResult Success(string? detail = null) => new(true, null, detail);

    public static ProviderTestResult Fail(string error) => new(false, error);
}

/// <summary>
/// What a returning shopper's callback settled.
/// </summary>
/// <remarks>
/// One shape for both things a gateway return can be about, because the
/// storefront's callback page gets only an authority back from ZarinPal and
/// cannot tell which it was until the API resolves it. <see cref="Kind"/> is
/// what the page branches on.
/// </remarks>
public sealed record PaymentCallbackResultDto(string Kind, string? OrderNumber, string? Reference, bool Paid)
{
    /// <summary>A wallet top-up. The balance moved, and the wallet screen is where to go.</summary>
    public const string Wallet = "wallet";

    /// <summary>An order. Its number is how the shopper finds it again.</summary>
    public const string Order = "order";
}
