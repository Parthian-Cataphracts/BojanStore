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
/// IDPay, over its v1.1 JSON API.
/// </summary>
/// <remarks>
/// <para>
/// Two things about this provider are unlike the other two, and both matter.
/// </para>
/// <para>
/// <b>It verifies on a pair.</b> Every other gateway here identifies a payment
/// by its own reference alone; IDPay wants its <c>id</c> *and* the
/// <c>order_id</c> the shop sent, and refuses a request naming only one. That
/// is why <see cref="Common.IPaymentGateway.VerifyAsync"/> carries the order
/// number at all.
/// </para>
/// <para>
/// <b>The verify window is ten minutes.</b> IDPay returns the money to the payer
/// if a paid transaction is not confirmed within that, which the storefront's
/// callback comfortably meets — but the reconciliation worker, which exists for
/// the shopper who closed the tab, does not. Its first sweep is fifteen minutes
/// after the order was placed, so on this provider a payment nobody came back
/// from is already refunded by the time it looks. That is the provider's design
/// rather than a defect here, and the worker's re-verify is still worth running:
/// it reads the outcome and stops the order sitting in limbo.
/// </para>
/// <para>
/// Amounts go out in Rial, like Zibal — IDPay has no currency parameter.
/// </para>
/// </remarks>
public sealed class IdPayPaymentGateway(
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<IdPayPaymentGateway> logger)
{
    private const string BaseUrl = "https://api.idpay.ir/v1.1";

    public const string HttpClientName = "idpay";

    /// <summary>Verified — the money is the shop's.</summary>
    private const int StatusVerified = 100;

    /// <summary>Verified before. A refreshed callback, or the worker arriving behind one.</summary>
    private const int StatusAlreadyVerified = 101;

    /// <summary>Settled to the merchant's account, which is also past paid.</summary>
    private const int StatusSettled = 200;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PaymentSession> StartAsync(
        string orderNumber,
        long amountToman,
        CancellationToken cancellationToken)
    {
        var (settings, apiKey) = await ReadSettingsAsync(cancellationToken);

        if (apiKey.Length == 0)
        {
            throw new InvalidOperationException("No IDPay API key is configured.");
        }

        var (document, status) = await PostAsync(
            "/payment",
            apiKey,
            settings.UseSandboxEndpoints,
            new RequestBody(
                orderNumber,
                ToRial(amountToman),
                Describe(settings.Description, orderNumber),
                settings.CallbackUrl),
            cancellationToken);

        var created = document.Deserialize<RequestData>(Json);

        if (created?.Id is not { Length: > 0 } id || created.Link is not { Length: > 0 } link)
        {
            var error = ReadError(document);

            logger.LogError(
                "IDPay refused a payment request for {OrderNumber}: HTTP {Status} {Code} {Message}",
                orderNumber,
                status,
                error.Code,
                error.Message);

            throw new InvalidOperationException($"IDPay returned {error.Code}: {Explain(error.Code, error.Message)}");
        }

        logger.LogInformation(
            "IDPay issued {Id} for {OrderNumber} ({Amount} Toman).",
            id,
            orderNumber,
            amountToman);

        // The redirect address is whatever IDPay hands back rather than one
        // built here: it differs between the live and sandbox accounts, and
        // guessing it is how a sandbox rehearsal silently redirects to nothing.
        return new PaymentSession(link, id);
    }

    public async Task<bool> VerifyAsync(
        string reference,
        string orderNumber,
        long amountToman,
        CancellationToken cancellationToken)
    {
        var (settings, apiKey) = await ReadSettingsAsync(cancellationToken);

        if (apiKey.Length == 0)
        {
            throw new InvalidOperationException("No IDPay API key is configured.");
        }

        var (document, httpStatus) = await PostAsync(
            "/payment/verify",
            apiKey,
            settings.UseSandboxEndpoints,
            new VerifyBody(reference, orderNumber),
            cancellationToken);

        var verified = document.Deserialize<VerifyData>(Json);

        if (verified?.Status is { } status && status is StatusVerified or StatusAlreadyVerified or StatusSettled)
        {
            // Checked rather than trusted, for the reason Zibal's is: the amount
            // is not part of what IDPay compares, so a session opened for a
            // different figure than the order is owed would otherwise settle it.
            var paid = verified.Payment?.Amount ?? verified.Amount;
            var expected = ToRial(amountToman);

            if (paid != expected)
            {
                logger.LogError(
                    "IDPay verified {Id} for {Paid} Rial, but {Expected} was owed. Not settling.",
                    reference,
                    paid,
                    expected);

                return false;
            }

            logger.LogInformation(
                "IDPay verified {Id} ({Amount} Rial), track {TrackId}, status {Status}.",
                reference,
                paid,
                verified.TrackId,
                status);

            return true;
        }

        var error = ReadError(document);

        logger.LogWarning(
            "IDPay did not verify {Id}: HTTP {Http} status {Status} code {Code} {Message}",
            reference,
            httpStatus,
            verified?.Status,
            error.Code,
            error.Message);

        return false;
    }

    /// <summary>
    /// The settings screen's test button.
    /// </summary>
    /// <remarks>
    /// Always through the sandbox, whatever the saved switch says, and this is
    /// the one provider where that is the right call: IDPay's live API has no
    /// harmless request. A real transaction would sit in the merchant's
    /// dashboard, and it cannot be cancelled — only left to expire. The sandbox
    /// answers with the same credential and the same validation of the callback
    /// domain, which is what the button is actually asking about.
    /// </remarks>
    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken)
    {
        var (settings, apiKey) = await ReadSettingsAsync(cancellationToken);

        if (apiKey.Length == 0)
        {
            return ProviderTestResult.Fail("کلید وب‌سرویس ثبت نشده است.");
        }

        if (!IsAbsoluteHttpUrl(settings.CallbackUrl))
        {
            return ProviderTestResult.Fail("آدرس بازگشت باید یک نشانی کامل با http یا https باشد.");
        }

        try
        {
            var (document, _) = await PostAsync(
                "/payment",
                apiKey,
                sandbox: true,
                new RequestBody(
                    $"TEST-{Guid.NewGuid():N}"[..20],
                    10_000,
                    "تست اتصال درگاه",
                    settings.CallbackUrl),
                cancellationToken);

            if (document.Deserialize<RequestData>(Json)?.Id is { Length: > 0 })
            {
                return ProviderTestResult.Success(
                    "کلید وب‌سرویس پذیرفته شد. این آزمایش همیشه روی محیط آزمایشی آیدی‌پی انجام می‌شود.");
            }

            var error = ReadError(document);
            return ProviderTestResult.Fail(Explain(error.Code, error.Message));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The IDPay connection test failed.");
            return ProviderTestResult.Fail("ارتباط با آیدی‌پی برقرار نشد. اتصال شبکه سرور را بررسی کنید.");
        }
    }

    /// <summary>
    /// Turns an IDPay error code into something an operator can act on.
    /// </summary>
    /// <remarks>
    /// IDPay already sends a Persian sentence in <c>error_message</c>, so the
    /// codes with an action behind them are rewritten and everything else keeps
    /// the provider's own wording rather than being flattened into one message.
    /// </remarks>
    private static string Explain(int code, string? message) => code switch
    {
        11 => "حساب کاربری آیدی‌پی مسدود شده است.",
        12 => "کلید وب‌سرویس پیدا نشد.",
        13 => "IP سرور با IPهای ثبت‌شده در وب‌سرویس همخوانی ندارد.",
        14 => "وب‌سرویس هنوز تأیید نشده یا در حال بررسی است.",
        21 => "حساب بانکی متصل به این وب‌سرویس تأیید نشده است.",
        24 => "حساب بانکی این وب‌سرویس غیرفعال شده است.",
        34 or 35 or 36 => "مبلغ تراکنش خارج از محدوده‌ی مجاز آیدی‌پی است.",
        38 or 39 => "دامنه‌ی آدرس بازگشت با آدرس ثبت‌شده در وب‌سرویس یکی نیست.",
        _ => message is { Length: > 0 } ? message : $"آیدی‌پی درخواست را نپذیرفت (کد {code}).",
    };

    /// <summary>Rial, which is what IDPay takes — see <c>ZibalPaymentGateway</c>.</summary>
    private static long ToRial(long toman) => toman * 10;

    /// <summary>
    /// Posts a body and hands back the document unread.
    /// </summary>
    /// <remarks>
    /// IDPay answers a success and a failure with entirely different shapes —
    /// <c>{id, link}</c> against <c>{error_code, error_message}</c> — rather than
    /// one envelope carrying a code, so there is nothing to normalise here and
    /// the caller reads whichever it expected.
    /// </remarks>
    private async Task<(JsonElement Document, int Status)> PostAsync(
        string path,
        string apiKey,
        bool sandbox,
        object body,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{path}")
        {
            Content = JsonContent.Create(body, options: Json),
        };

        // Per request rather than on the pooled client's default headers: the
        // key is read per payment, so a key changed in the panel would otherwise
        // keep sending under the old one until the handler was recycled.
        request.Headers.Add("X-API-KEY", apiKey);
        if (sandbox) request.Headers.Add("X-SANDBOX", "1");

        using var response = await client.SendAsync(request, cancellationToken);

        // Not EnsureSuccessStatusCode: IDPay answers a rejected request with a
        // non-2xx and a body that says why, and that body is the whole reason
        // the operator can be told which thing is wrong.
        var document = await GatewayHttp.ReadJsonAsync(response, "IDPay", cancellationToken);

        return (document, (int)response.StatusCode);
    }

    private static (int Code, string? Message) ReadError(JsonElement document)
    {
        var code = document.TryGetProperty("error_code", out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : 0;

        var message = document.TryGetProperty("error_message", out var text) && text.ValueKind is JsonValueKind.String
            ? text.GetString()
            : null;

        return (code, message);
    }

    private static string Describe(string prefix, string reference)
    {
        var text = prefix is { Length: > 0 } ? $"{prefix} — {reference}" : $"پرداخت سفارش {reference}";
        return text.Length <= 255 ? text : text[..255];
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private async Task<(PaymentGatewaySettingsDto Settings, string ApiKey)> ReadSettingsAsync(
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<PaymentGatewaySettingsStore>();
        return await store.GetWithMerchantIdAsync(cancellationToken);
    }

    private sealed record RequestBody(
        [property: JsonPropertyName("order_id")] string OrderId,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("desc")] string Description,
        [property: JsonPropertyName("callback")] string Callback);

    private sealed record RequestData(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("link")] string? Link);

    private sealed record VerifyBody(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("order_id")] string OrderId);

    /// <remarks>
    /// Every numeric field arrives as a JSON string — <c>"status": "100"</c> —
    /// so they are read as strings and parsed, which is what
    /// <see cref="JsonNumberHandling.AllowReadingFromString"/> does here.
    /// </remarks>
    private sealed record VerifyData(
        [property: JsonPropertyName("status")]
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        int? Status,
        [property: JsonPropertyName("track_id")]
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        long TrackId,
        [property: JsonPropertyName("amount")]
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        long Amount,
        [property: JsonPropertyName("payment")] VerifyPayment? Payment);

    private sealed record VerifyPayment(
        [property: JsonPropertyName("amount")]
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        long Amount);
}
