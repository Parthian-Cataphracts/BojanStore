using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bojan.Application.Auth;
using Bojan.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Notifications;

/// <summary>
/// SMS.ir, over its v1 REST API.
/// </summary>
/// <remarks>
/// <para>
/// Two endpoints for the two things this shop sends.
/// <see cref="SendVerificationAsync"/> posts to <c>/v1/send/verify</c>, which
/// takes a template id and named parameters rather than a message: the wording
/// is registered and approved on SMS.ir's side, it goes out on a service line
/// at high priority, and — the part that matters — it reaches numbers that have
/// blocked advertising SMS. <see cref="SendAsync"/> posts free text to
/// <c>/v1/send/bulk</c> from the shop's own line, which is right for a campaign
/// and wrong for a sign-in code.
/// </para>
/// <para>
/// Every response carries a <c>status</c> that is <c>1</c> for success and a
/// code for everything else, and the HTTP status alone does not tell them
/// apart cleanly — so the body is what is read. The codes worth a sentence get
/// one, because "اعتبار کافی نیست" is something an operator fixes in ten
/// seconds and "ارسال نشد" is something they open a ticket about.
/// </para>
/// <para>
/// Nothing here throws at the caller. Both callers are on paths where a failed
/// message must not fail the request: an OTP request that throws tells the
/// shopper their number is broken when the truth is that the shop's SMS credit
/// ran out, and a campaign fan-out that throws stops the recipients after it.
/// Failures are logged, and the settings screen's test button is the place a
/// person gets told directly.
/// </para>
/// </remarks>
public sealed class SmsIrSender(
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<SmsIrSender> logger) : ISmsSender
{
    public const string HttpClientName = "smsir";

    private const string BaseUrl = "https://api.sms.ir";

    /// <summary>SMS.ir's "operation succeeded".</summary>
    private const int StatusSuccess = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task SendAsync(string phone, string message, CancellationToken cancellationToken)
    {
        var result = await SendBulkAsync(phone, message, cancellationToken);

        if (!result.Ok)
        {
            logger.LogError("SMS.ir refused a message to {Phone}: {Error}", Mask(phone), result.Error);
        }
    }

    public async Task SendVerificationAsync(string phone, string code, CancellationToken cancellationToken)
    {
        var result = await SendVerifyAsync(phone, code, cancellationToken);

        if (!result.Ok)
        {
            // The code itself is never in this line. It is the credential the
            // message exists to deliver, and a log that carries it is a log
            // that signs anyone in.
            logger.LogError("SMS.ir refused a sign-in code to {Phone}: {Error}", Mask(phone), result.Error);
        }
    }

    /// <summary>
    /// The settings screen's test button.
    /// </summary>
    /// <remarks>
    /// Sends through the verification template rather than the bulk line,
    /// because that is the path a broken configuration breaks silently: a
    /// missing template id or a parameter name that does not match the template
    /// stops sign-in and nothing else notices. The bulk line failing is visible
    /// the first time a campaign goes out.
    /// </remarks>
    public async Task<ProviderTestResult> TestAsync(string phone, CancellationToken cancellationToken)
    {
        var (settings, apiKey) = await ReadSettingsAsync(cancellationToken);

        if (apiKey.Length == 0)
        {
            return ProviderTestResult.Fail("کلید وب‌سرویس ثبت نشده است.");
        }

        if (settings.OtpTemplateId <= 0)
        {
            return ProviderTestResult.Fail("شناسه قالب کد تأیید ثبت نشده است.");
        }

        var normalised = Normalise(phone);
        if (normalised is null)
        {
            return ProviderTestResult.Fail("شماره موبایل معتبر نیست.");
        }

        var result = await SendVerifyAsync(normalised, "12345", cancellationToken);

        return result.Ok
            ? ProviderTestResult.Success($"پیامک آزمایشی به {phone} ارسال شد.")
            : ProviderTestResult.Fail(result.Error!);
    }

    private async Task<ProviderTestResult> SendVerifyAsync(
        string phone,
        string code,
        CancellationToken cancellationToken)
    {
        var (settings, apiKey) = await ReadSettingsAsync(cancellationToken);

        if (apiKey.Length == 0 || settings.OtpTemplateId <= 0)
        {
            return ProviderTestResult.Fail("سرویس پیامک هنوز کامل تنظیم نشده است.");
        }

        var normalised = Normalise(phone);
        if (normalised is null)
        {
            return ProviderTestResult.Fail("شماره موبایل معتبر نیست.");
        }

        return await PostAsync(
            "/v1/send/verify",
            apiKey,
            new VerifyBody(
                normalised,
                settings.OtpTemplateId,
                [new TemplateParameter(settings.OtpParameterName, code)]),
            cancellationToken);
    }

    private async Task<ProviderTestResult> SendBulkAsync(
        string phone,
        string message,
        CancellationToken cancellationToken)
    {
        var (settings, apiKey) = await ReadSettingsAsync(cancellationToken);

        if (apiKey.Length == 0)
        {
            return ProviderTestResult.Fail("کلید وب‌سرویس ثبت نشده است.");
        }

        if (!long.TryParse(settings.LineNumber, out var line) || line <= 0)
        {
            return ProviderTestResult.Fail("شماره خط ارسال ثبت نشده است.");
        }

        var normalised = Normalise(phone);
        if (normalised is null)
        {
            return ProviderTestResult.Fail("شماره موبایل معتبر نیست.");
        }

        return await PostAsync(
            "/v1/send/bulk",
            apiKey,
            new BulkBody(line, message, [normalised]),
            cancellationToken);
    }

    /// <summary>
    /// Posts a body and reads back SMS.ir's envelope.
    /// </summary>
    /// <remarks>
    /// Never throws. Every failure — a refused status, a body that does not
    /// parse, a socket that never connected — comes back as a result, because
    /// the two production callers are both on paths where a thrown exception
    /// would break something larger than one message.
    /// </remarks>
    private async Task<ProviderTestResult> PostAsync(
        string path,
        string apiKey,
        object body,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{path}")
            {
                Content = JsonContent.Create(body, options: Json),
            };

            // Per request rather than on the client's default headers: the
            // client is pooled across the process and the key is read per
            // message, so a key changed in the panel would otherwise keep
            // sending under the old one until the handler was recycled.
            request.Headers.Add("X-API-KEY", apiKey);

            using var response = await client.SendAsync(request, cancellationToken);

            var document = await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);

            if (document.ValueKind is not JsonValueKind.Object
                || !document.TryGetProperty("status", out var statusValue)
                || !statusValue.TryGetInt32(out var status))
            {
                return ProviderTestResult.Fail(
                    $"پاسخ نامفهوم از سرویس پیامک (HTTP {(int)response.StatusCode}).");
            }

            if (status == StatusSuccess)
            {
                return ProviderTestResult.Success();
            }

            var message = document.TryGetProperty("message", out var text) && text.ValueKind is JsonValueKind.String
                ? text.GetString()
                : null;

            return ProviderTestResult.Fail(Explain(status, message));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderTestResult.Fail("سرویس پیامک در زمان تعیین‌شده پاسخ نداد.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The SMS.ir request to {Path} failed.", path);
            return ProviderTestResult.Fail("ارتباط با سرویس پیامک برقرار نشد.");
        }
    }

    /// <summary>
    /// Turns an SMS.ir status code into something an operator can act on.
    /// </summary>
    /// <remarks>
    /// Only the codes with an action behind them get their own sentence. The
    /// rest fall through carrying the code and the provider's own message,
    /// which is what their support will ask for.
    /// </remarks>
    private static string Explain(int status, string? message) => status switch
    {
        10 => "کلید وب‌سرویس معتبر نیست.",
        11 => "کلید وب‌سرویس غیرفعال است.",
        12 => "کلید وب‌سرویس فقط از IPهای مشخصی قابل استفاده است؛ IP سرور را در پنل sms.ir مجاز کنید.",
        13 => "حساب کاربری sms.ir غیرفعال است.",
        14 => "حساب کاربری sms.ir در حالت تعلیق است.",
        20 => "تعداد درخواست‌ها بیش از حد مجاز بوده است.",
        101 => "شماره خط ارسال معتبر نیست.",
        102 => "اعتبار پیامک کافی نیست.",
        104 => "شماره موبایل نادرست است.",
        113 => "قالب پیامک با این شناسه پیدا نشد.",
        114 => "مقدار پارامتر قالب بیش از ۲۵ کاراکتر است.",
        115 => "این شماره در لیست سیاه سامانه است.",
        116 => "نام پارامتر قالب خالی است.",
        117 => "متن پیامک تأیید نشده است.",
        123 => "خط ارسال‌کننده نیاز به فعال‌سازی دارد.",
        _ => $"سرویس پیامک درخواست را نپذیرفت (کد {status}){(message is { Length: > 0 } ? $": {message}" : ".")}",
    };

    /// <summary>
    /// Puts a number into the shape SMS.ir accepts.
    /// </summary>
    /// <remarks>
    /// The shop stores numbers as <c>09xxxxxxxxx</c>; SMS.ir's own samples use
    /// both that and the bare <c>9xxxxxxxxx</c>. The leading zero is dropped
    /// because that is the form their documentation uses for every endpoint,
    /// and <c>+98</c> and <c>0098</c> prefixes are folded in because an
    /// operator typing their own number into the test box will use one of them.
    /// </remarks>
    private static string? Normalise(string phone)
    {
        var digits = new string([.. phone.Where(char.IsAsciiDigit)]);

        digits = digits switch
        {
            ['0', '0', '9', '8', .. var rest] => rest,
            ['9', '8', '9', .. var rest] => $"9{rest}",
            ['0', '9', .. var rest] => $"9{rest}",
            _ => digits,
        };

        return digits.Length == 10 && digits[0] == '9' ? digits : null;
    }

    /// <summary>Enough of the number to identify a log line, not enough to be a contact list.</summary>
    private static string Mask(string phone) =>
        phone.Length <= 4 ? "***" : $"{phone[..4]}***{phone[^2..]}";

    private async Task<(SmsSettingsDto Settings, string ApiKey)> ReadSettingsAsync(
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<SmsSettingsStore>();
        return await store.GetWithApiKeyAsync(cancellationToken);
    }

    private sealed record VerifyBody(
        [property: JsonPropertyName("mobile")] string Mobile,
        [property: JsonPropertyName("templateId")] int TemplateId,
        [property: JsonPropertyName("parameters")] IReadOnlyList<TemplateParameter> Parameters);

    private sealed record TemplateParameter(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("value")] string Value);

    private sealed record BulkBody(
        [property: JsonPropertyName("lineNumber")] long LineNumber,
        [property: JsonPropertyName("messageText")] string MessageText,
        [property: JsonPropertyName("mobiles")] IReadOnlyList<string> Mobiles);
}
