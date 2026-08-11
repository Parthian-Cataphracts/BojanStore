using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Payments;

/// <summary>
/// Zibal, over its v1 JSON API.
/// </summary>
/// <remarks>
/// <para>
/// The same three steps every gateway here follows: ask for a session, send the
/// shopper to it, and afterwards ask whether the money arrived. Zibal calls the
/// session a <c>trackId</c>, and it is what this stores as the payment
/// reference.
/// </para>
/// <para>
/// <b>Amounts go out in Rial.</b> Zibal has no currency parameter — it takes
/// Rial and nothing else — so the Toman this shop stores are multiplied by ten
/// here, at the edge, where the conversion is next to the sentence explaining
/// it. Getting this wrong in either direction is a factor-of-ten error in what
/// the customer is charged, and nobody notices until the reconciliation.
/// </para>
/// <para>
/// Verification treats result 201 like 100. Zibal answers 100 the first time a
/// trackId is verified and 201 on every attempt after, so a shopper who
/// refreshes the callback — or the reconciliation worker arriving behind it —
/// gets 201 for a payment that genuinely happened.
/// </para>
/// <para>
/// The callback's <c>success</c> and <c>status</c> parameters are deliberately
/// not read anywhere: they arrive in the shopper's own browser. Only this
/// verification decides.
/// </para>
/// </remarks>
public sealed class ZibalPaymentGateway(
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<ZibalPaymentGateway> logger)
{
    private const string BaseUrl = "https://gateway.zibal.ir";

    /// <summary>
    /// The merchant Zibal publishes for testing.
    /// </summary>
    /// <remarks>
    /// Unlike ZarinPal there is no separate sandbox host: the same endpoints
    /// answer, and this merchant is what makes them simulate rather than
    /// charge. So the sandbox switch on the settings screen substitutes the
    /// credential instead of the URL.
    /// </remarks>
    private const string SandboxMerchant = "zibal";

    public const string HttpClientName = "zibal";

    /// <summary>Operation succeeded.</summary>
    private const int ResultSuccess = 100;

    /// <summary>Already verified — a success reported before.</summary>
    private const int ResultAlreadyVerified = 201;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PaymentSession> StartAsync(
        string orderNumber,
        long amountToman,
        CancellationToken cancellationToken)
    {
        var (settings, merchant) = await ReadSettingsAsync(cancellationToken);

        if (merchant.Length == 0)
        {
            throw new InvalidOperationException("No Zibal merchant is configured.");
        }

        var response = await PostAsync<RequestData>(
            $"{BaseUrl}/v1/request",
            new RequestBody(
                merchant,
                ToRial(amountToman),
                settings.CallbackUrl,
                Describe(settings.Description, orderNumber),
                orderNumber),
            cancellationToken);

        if (response.Result != ResultSuccess || response.Data?.TrackId is not { } trackId || trackId == 0)
        {
            logger.LogError(
                "Zibal refused a payment request for {OrderNumber}: {Result} {Message}",
                orderNumber,
                response.Result,
                response.Message);

            throw new InvalidOperationException($"Zibal returned {response.Result}: {Explain(response.Result)}");
        }

        logger.LogInformation(
            "Zibal issued trackId {TrackId} for {OrderNumber} ({Amount} Toman).",
            trackId,
            orderNumber,
            amountToman);

        var reference = trackId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new PaymentSession($"{BaseUrl}/start/{reference}", reference);
    }

    public async Task<bool> VerifyAsync(
        string reference,
        string orderNumber,
        long amountToman,
        CancellationToken cancellationToken)
    {
        var (_, merchant) = await ReadSettingsAsync(cancellationToken);

        if (merchant.Length == 0)
        {
            throw new InvalidOperationException("No Zibal merchant is configured.");
        }

        if (!long.TryParse(reference, out var trackId))
        {
            throw new InvalidOperationException($"'{reference}' is not a Zibal trackId.");
        }

        var response = await PostAsync<VerifyData>(
            $"{BaseUrl}/v1/verify",
            new VerifyBody(merchant, trackId),
            cancellationToken);

        if (response.Result is ResultSuccess or ResultAlreadyVerified)
        {
            // The amount is checked here rather than trusted, because Zibal's
            // verify takes only the trackId — there is no amount to send, so
            // nothing on their side compares one. A session opened for a
            // different figure than the order is owed would otherwise settle it.
            var paid = response.Data?.Amount ?? 0;
            var expected = ToRial(amountToman);

            if (paid != expected)
            {
                logger.LogError(
                    "Zibal verified {TrackId} for {Paid} Rial, but {Expected} was owed. Not settling.",
                    trackId,
                    paid,
                    expected);

                return false;
            }

            logger.LogInformation(
                "Zibal verified {TrackId} ({Amount} Rial), ref {RefNumber}, result {Result}.",
                trackId,
                paid,
                response.Data?.RefNumber,
                response.Result);

            return true;
        }

        // 202 is the ordinary "this was never paid" — a shopper who reached the
        // gateway and abandoned it, which is most unfinished sessions.
        logger.LogWarning(
            "Zibal did not verify {TrackId}: {Result} {Message}",
            trackId,
            response.Result,
            response.Message);

        return false;
    }

    /// <summary>
    /// The settings screen's test button.
    /// </summary>
    /// <remarks>
    /// A payment request for the smallest amount Zibal accepts, never redirected
    /// to and never verified, so nobody is charged and the session expires
    /// unused. Result 100 means the merchant was recognised and the callback was
    /// acceptable.
    /// </remarks>
    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken)
    {
        var (settings, merchant) = await ReadSettingsAsync(cancellationToken);

        if (merchant.Length == 0)
        {
            return ProviderTestResult.Fail("شناسه پذیرنده ثبت نشده است.");
        }

        if (!IsAbsoluteHttpUrl(settings.CallbackUrl))
        {
            return ProviderTestResult.Fail("آدرس بازگشت باید یک نشانی کامل با http یا https باشد.");
        }

        try
        {
            var response = await PostAsync<RequestData>(
                $"{BaseUrl}/v1/request",
                new RequestBody(merchant, 10_000, settings.CallbackUrl, "تست اتصال درگاه", "TEST"),
                cancellationToken);

            return response.Result == ResultSuccess
                ? ProviderTestResult.Success(
                    settings.UseSandboxEndpoints
                        ? "اتصال به حساب آزمایشی زیبال برقرار است."
                        : "اتصال به زیبال برقرار است و شناسه پذیرنده پذیرفته شد.")
                : ProviderTestResult.Fail(Explain(response.Result));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The Zibal connection test failed.");
            return ProviderTestResult.Fail("ارتباط با زیبال برقرار نشد. اتصال شبکه سرور را بررسی کنید.");
        }
    }

    /// <summary>Turns a Zibal result code into something an operator can act on.</summary>
    private static string Explain(int result) => result switch
    {
        102 => "شناسه پذیرنده پیدا نشد.",
        103 => "شناسه پذیرنده غیرفعال است یا قرارداد درگاه امضا نشده.",
        104 => "شناسه پذیرنده نامعتبر است.",
        105 => "مبلغ باید بیشتر از ۱۰۰ تومان باشد.",
        106 => "آدرس بازگشت نامعتبر است — باید با http یا https شروع شود.",
        113 => "مبلغ تراکنش از سقف مجاز درگاه بیشتر است.",
        115 => "IP سرور در پنل زیبال ثبت نشده است.",
        202 => "این تراکنش پرداخت نشده یا ناموفق بوده است.",
        203 => "شناسه پیگیری نامعتبر است.",
        _ => $"زیبال درخواست را نپذیرفت (کد {result}).",
    };

    /// <summary>
    /// Rial, which is what Zibal takes.
    /// </summary>
    /// <remarks>
    /// Everything in this shop is stored, displayed and reasoned about in Toman.
    /// The conversion lives here rather than at the call site so there is one
    /// place per gateway where the unit changes, next to the note saying why.
    /// </remarks>
    private static long ToRial(long toman) => toman * 10;

    private async Task<Envelope<T>> PostAsync<T>(string url, object body, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.PostAsJsonAsync(url, body, Json, cancellationToken);

        var document = await GatewayHttp.ReadJsonAsync(response, "Zibal", cancellationToken);

        if (!document.TryGetProperty("result", out var resultValue) || !resultValue.TryGetInt32(out var result))
        {
            throw new InvalidOperationException(
                $"Zibal answered {(int)response.StatusCode} without a result code.");
        }

        var message = document.TryGetProperty("message", out var text) && text.ValueKind is JsonValueKind.String
            ? text.GetString()
            : null;

        // The payload is flat rather than nested under `data`, so the whole
        // document is what deserializes into the shape the caller wanted.
        return new Envelope<T>(result, message, document.Deserialize<T>(Json));
    }

    /// <summary>
    /// The description shown on the bank's confirmation page.
    /// </summary>
    /// <remarks>
    /// The shopper reads this while deciding whether to enter a card number, so
    /// the reference goes in it. The operator's own wording is the prefix.
    /// </remarks>
    private static string Describe(string prefix, string reference)
    {
        var text = prefix is { Length: > 0 } ? $"{prefix} — {reference}" : $"پرداخت سفارش {reference}";
        return text.Length <= 255 ? text : text[..255];
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    /// <remarks>
    /// Read per call through a scope, so a merchant entered in the panel applies
    /// to the next payment rather than the next restart — see
    /// <c>ZarinPalPaymentGateway</c> for the same arrangement.
    /// </remarks>
    private async Task<(PaymentGatewaySettingsDto Settings, string Merchant)> ReadSettingsAsync(
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<PaymentGatewaySettingsStore>();
        var (settings, credential) = await store.GetWithMerchantIdAsync(cancellationToken);

        // The sandbox switch swaps the credential rather than the host, because
        // that is how Zibal's test account works.
        return (settings, settings.UseSandboxEndpoints ? SandboxMerchant : credential);
    }

    private sealed record Envelope<T>(int Result, string? Message, T? Data);

    private sealed record RequestBody(
        [property: JsonPropertyName("merchant")] string Merchant,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("callbackUrl")] string CallbackUrl,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("orderId")] string OrderId);

    private sealed record RequestData([property: JsonPropertyName("trackId")] long TrackId);

    private sealed record VerifyBody(
        [property: JsonPropertyName("merchant")] string Merchant,
        [property: JsonPropertyName("trackId")] long TrackId);

    private sealed record VerifyData(
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("refNumber")] long RefNumber,
        [property: JsonPropertyName("status")] int Status);
}
