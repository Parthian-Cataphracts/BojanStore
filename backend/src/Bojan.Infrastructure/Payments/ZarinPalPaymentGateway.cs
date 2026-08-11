using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Payments;

/// <summary>
/// ZarinPal, over its v4 JSON API.
/// </summary>
/// <remarks>
/// <para>
/// Two calls make a payment. <see cref="StartAsync"/> posts the amount and a
/// callback address and gets an <c>authority</c> back, which is both the id of
/// the attempt and the last segment of the URL the shopper is redirected to.
/// <see cref="VerifyAsync"/> posts that authority back with the amount and is
/// the only thing that decides whether money arrived — a shopper returning with
/// <c>Status=OK</c> in a query string has proved nothing, because a query
/// string is written by whoever is holding the browser.
/// </para>
/// <para>
/// Amounts go out as <c>IRT</c>. Everything in this shop is stored and
/// displayed in Toman, and the alternative — converting to Rial at the edge —
/// puts a factor of ten between what the customer was quoted and what the bank
/// charges, in a direction nobody notices until the reconciliation.
/// </para>
/// <para>
/// The merchant id is read per call rather than captured once, so a change made
/// in the panel takes effect on the next payment instead of the next deploy. It
/// is never logged: it is the credential that lets money be requested in this
/// shop's name, and the diagnostic value of having it in a log file is zero.
/// </para>
/// <para>
/// Verification treats code 101 exactly like 100. ZarinPal returns 100 the
/// first time an authority is verified and 101 on every attempt after, so a
/// shopper who refreshes the callback page — or the reconciliation worker
/// arriving after the callback already settled the order — gets 101 for a
/// payment that genuinely happened. Reading 101 as failure would mark paid
/// orders unpaid.
/// </para>
/// </remarks>
public sealed class ZarinPalPaymentGateway(
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<ZarinPalPaymentGateway> logger)
{
    /// <summary>The live host. Sandbox swaps the host and nothing else — see the ZarinPal docs.</summary>
    private const string LiveBaseUrl = "https://payment.zarinpal.com";

    private const string SandboxBaseUrl = "https://sandbox.zarinpal.com";

    /// <summary>The named client, so the timeout and retry policy live in one place.</summary>
    public const string HttpClientName = "zarinpal";

    /// <summary>Operation succeeded.</summary>
    private const int CodeSuccess = 100;

    /// <summary>Already verified — a success that has been reported before.</summary>
    private const int CodeAlreadyVerified = 101;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Asks for an authority and builds the URL the shopper is sent to.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The gateway refused the request or could not be reached. Thrown rather
    /// than returned because the callers — checkout and wallet top-up — both
    /// already treat a failure to start as "the order exists, the redirect does
    /// not", which is a different outcome from a declined payment.
    /// </exception>
    public async Task<PaymentSession> StartAsync(
        string reference,
        long amountToman,
        CancellationToken cancellationToken)
    {
        var (settings, merchantId) = await ReadSettingsAsync(cancellationToken);

        if (merchantId.Length == 0)
        {
            throw new InvalidOperationException("No ZarinPal merchant id is configured.");
        }

        var baseUrl = settings.UseSandboxEndpoints ? SandboxBaseUrl : LiveBaseUrl;

        // The order or top-up reference travels as ZarinPal's own order_id
        // rather than in the callback URL. The callback address is checked
        // against the domain registered on the terminal, so a per-payment query
        // string is one more thing that can make a correct configuration look
        // broken — and the authority coming back is what identifies the payment
        // anyway.
        var request = new RequestBody(
            merchantId,
            amountToman,
            "IRT",
            Describe(settings.Description, reference),
            settings.CallbackUrl,
            new RequestMetadata(reference));

        var response = await PostAsync<RequestData>(
            $"{baseUrl}/pg/v4/payment/request.json",
            request,
            cancellationToken);

        if (response.Code != CodeSuccess || string.IsNullOrWhiteSpace(response.Data?.Authority))
        {
            logger.LogError(
                "ZarinPal refused a payment request for {Reference}: {Code} {Message}",
                reference,
                response.Code,
                response.Message);

            throw new InvalidOperationException($"ZarinPal returned {response.Code}: {response.Message}");
        }

        var authority = response.Data.Authority;

        logger.LogInformation(
            "ZarinPal issued authority {Authority} for {Reference} ({Amount} Toman).",
            authority,
            reference,
            amountToman);

        return new PaymentSession($"{baseUrl}/pg/StartPay/{Uri.EscapeDataString(authority)}", authority);
    }

    /// <summary>
    /// Asks whether the money for an authority actually arrived.
    /// </summary>
    /// <remarks>
    /// <paramref name="orderNumber"/> is unused: ZarinPal identifies a payment
    /// by its authority alone. It is on the port because IDPay does not — see
    /// <c>IdPayPaymentGateway</c>.
    ///
    /// Returns false rather than throwing for a refusal, because a declined
    /// payment is an ordinary outcome the callers have somewhere to put. It
    /// throws only when the answer is unknown — the gateway could not be
    /// reached, or answered something that is not a verification — because
    /// "unknown" and "no" must not settle to the same thing on a path where
    /// "no" is written to an order.
    /// </remarks>
    public async Task<bool> VerifyAsync(
        string reference,
        string orderNumber,
        long amountToman,
        CancellationToken cancellationToken)
    {
        var (settings, merchantId) = await ReadSettingsAsync(cancellationToken);

        if (merchantId.Length == 0)
        {
            throw new InvalidOperationException("No ZarinPal merchant id is configured.");
        }

        var baseUrl = settings.UseSandboxEndpoints ? SandboxBaseUrl : LiveBaseUrl;

        var response = await PostAsync<VerifyData>(
            $"{baseUrl}/pg/v4/payment/verify.json",
            new VerifyBody(merchantId, amountToman, reference),
            cancellationToken);

        if (response.Code is CodeSuccess or CodeAlreadyVerified)
        {
            logger.LogInformation(
                "ZarinPal verified {Authority} ({Amount} Toman), ref_id {RefId}, code {Code}.",
                reference,
                amountToman,
                response.Data?.RefId,
                response.Code);

            return true;
        }

        // -51 is the ordinary "this was never paid": a shopper who reached the
        // gateway and abandoned it, which is most abandoned authorities. -50 is
        // the one worth staring at, because it means an amount mismatch.
        logger.LogWarning(
            "ZarinPal did not verify {Authority} ({Amount} Toman): {Code} {Message}",
            reference,
            amountToman,
            response.Code,
            response.Message);

        return false;
    }

    /// <summary>
    /// The settings screen's test button.
    /// </summary>
    /// <remarks>
    /// There is no "check my credentials" method in the API, so this asks the
    /// question the merchant id has to answer anyway — a one-Toman payment
    /// request, never redirected to and never verified, which expires unused.
    /// Code 100 means the terminal recognised the merchant id, the callback
    /// domain matched and the account is active; every other code is a specific
    /// sentence the operator can act on.
    /// </remarks>
    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken)
    {
        var (settings, merchantId) = await ReadSettingsAsync(cancellationToken);

        if (merchantId.Length == 0)
        {
            return ProviderTestResult.Fail("شناسه پذیرنده ثبت نشده است.");
        }

        if (!IsAbsoluteHttpUrl(settings.CallbackUrl))
        {
            return ProviderTestResult.Fail("آدرس بازگشت باید یک نشانی کامل با http یا https باشد.");
        }

        var baseUrl = settings.UseSandboxEndpoints ? SandboxBaseUrl : LiveBaseUrl;

        try
        {
            var response = await PostAsync<RequestData>(
                $"{baseUrl}/pg/v4/payment/request.json",
                new RequestBody(
                    merchantId,
                    // The smallest amount ZarinPal will accept a request for.
                    // Nothing is ever redirected here, so nobody is charged.
                    1000,
                    "IRT",
                    "تست اتصال درگاه",
                    settings.CallbackUrl,
                    new RequestMetadata("TEST")),
                cancellationToken);

            return response.Code == CodeSuccess
                ? ProviderTestResult.Success(
                    settings.UseSandboxEndpoints
                        ? "اتصال به سرویس تست زرین‌پال برقرار است."
                        : "اتصال به زرین‌پال برقرار است و شناسه پذیرنده پذیرفته شد.")
                : ProviderTestResult.Fail(Explain(response.Code, response.Message));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The ZarinPal connection test failed.");
            return ProviderTestResult.Fail("ارتباط با زرین‌پال برقرار نشد. اتصال شبکه سرور را بررسی کنید.");
        }
    }

    /// <summary>
    /// Turns a ZarinPal error code into something an operator can act on.
    /// </summary>
    /// <remarks>
    /// Only the codes an operator can actually do something about get their own
    /// sentence. The rest fall through carrying the code, which is what
    /// ZarinPal's support will ask for.
    /// </remarks>
    private static string Explain(int code, string? message) => code switch
    {
        -9 => "اطلاعات ارسالی ناقص یا نامعتبر است — آدرس بازگشت و توضیحات را بررسی کنید.",
        -10 => "شناسه پذیرنده یا IP سرور درست نیست.",
        -11 => "این شناسه پذیرنده هنوز فعال نشده است.",
        -12 => "تعداد درخواست‌ها در یک بازه‌ی کوتاه بیش از حد مجاز بوده است.",
        -13 => "درگاه به سقف تراکنش رسیده است؛ مدارک پذیرنده باید تکمیل شود.",
        -14 => "آدرس بازگشت با دامنه‌ی ثبت‌شده‌ی درگاه یکی نیست.",
        -15 => "این درگاه در حالت تعلیق است.",
        -16 or -17 => "سطح تأیید حساب پذیرنده برای استفاده از درگاه کافی نیست.",
        -19 => "امکان ایجاد تراکنش برای این درگاه وجود ندارد.",
        _ => $"زرین‌پال درخواست را نپذیرفت (کد {code}){(message is { Length: > 0 } ? $": {message}" : ".")}",
    };

    /// <summary>
    /// Posts a body and reads back ZarinPal's envelope.
    /// </summary>
    /// <remarks>
    /// The envelope is awkward: <c>errors</c> is an empty array on success and
    /// an object on failure, so a typed property for it fails to deserialize on
    /// whichever shape it was not written for. Reading the document as
    /// <see cref="JsonElement"/> and pulling the two branches out by hand is
    /// what keeps a declined payment from arriving as a deserialization
    /// exception — which the callers would read as "unknown" rather than "no".
    /// </remarks>
    private async Task<Envelope<T>> PostAsync<T>(string url, object body, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.PostAsJsonAsync(url, body, Json, cancellationToken);

        // Not EnsureSuccessStatusCode: ZarinPal answers a rejected request with
        // a non-2xx status and a body that says why, and that body is the whole
        // reason the operator can be told which thing is wrong.
        var document = await GatewayHttp.ReadJsonAsync(response, "ZarinPal", cancellationToken);

        if (document.TryGetProperty("data", out var data)
            && data.ValueKind is JsonValueKind.Object
            && data.TryGetProperty("code", out var dataCode)
            && dataCode.TryGetInt32(out var code)
            && code is CodeSuccess or CodeAlreadyVerified)
        {
            return new Envelope<T>(code, ReadString(data, "message"), data.Deserialize<T>(Json));
        }

        // Failures arrive under `errors`, and occasionally under `data` with a
        // negative code. Both are read, in that order.
        if (document.TryGetProperty("errors", out var errors) && errors.ValueKind is JsonValueKind.Object)
        {
            return new Envelope<T>(
                errors.TryGetProperty("code", out var errorCode) && errorCode.TryGetInt32(out var value) ? value : 0,
                ReadString(errors, "message"),
                default);
        }

        if (data.ValueKind is JsonValueKind.Object
            && data.TryGetProperty("code", out var fallbackCode)
            && fallbackCode.TryGetInt32(out var fallback))
        {
            return new Envelope<T>(fallback, ReadString(data, "message"), default);
        }

        throw new InvalidOperationException($"ZarinPal answered {(int)response.StatusCode} with no recognisable outcome.");
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// The description shown on the bank's confirmation page.
    /// </summary>
    /// <remarks>
    /// ZarinPal rejects a request whose description is empty or over five
    /// hundred characters, and the shopper reads this line while deciding
    /// whether to enter a card number — so the reference goes in it. The
    /// operator's own wording is the prefix.
    /// </remarks>
    private static string Describe(string prefix, string reference)
    {
        var text = prefix is { Length: > 0 } ? $"{prefix} — {reference}" : $"پرداخت سفارش {reference}";
        return text.Length <= 500 ? text : text[..500];
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";

    /// <summary>
    /// Reads the stored settings.
    /// </summary>
    /// <remarks>
    /// Through a scope because the store is scoped over the database context
    /// while this is a singleton — the same arrangement
    /// <c>SmtpEmailSender</c> uses to read the mailbox account per message,
    /// and for the same reason: settings changed in the panel have to apply to
    /// the next payment, not the next restart.
    /// </remarks>
    private async Task<(PaymentGatewaySettingsDto Settings, string MerchantId)> ReadSettingsAsync(
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<PaymentGatewaySettingsStore>();
        return await store.GetWithMerchantIdAsync(cancellationToken);
    }

    private sealed record Envelope<T>(int Code, string? Message, T? Data);

    private sealed record RequestBody(
        [property: JsonPropertyName("merchant_id")] string MerchantId,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("callback_url")] string CallbackUrl,
        [property: JsonPropertyName("metadata")] RequestMetadata Metadata);

    private sealed record RequestMetadata([property: JsonPropertyName("order_id")] string OrderId);

    private sealed record RequestData([property: JsonPropertyName("authority")] string Authority);

    private sealed record VerifyBody(
        [property: JsonPropertyName("merchant_id")] string MerchantId,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("authority")] string Authority);

    private sealed record VerifyData([property: JsonPropertyName("ref_id")] long RefId);
}
