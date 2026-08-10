namespace Bojan.Application.Contracts;

/// <summary>Which SMS service the shop sends through.</summary>
public static class SmsProviders
{
    /// <summary>None. Messages are dropped and said to have been dropped.</summary>
    public const string None = "none";

    /// <summary>SMS.ir, over its v1 REST API.</summary>
    public const string SmsIr = "smsir";

    public static bool IsKnown(string value) => value is None or SmsIr;
}

/// <summary>
/// The SMS account, as the panel sees it.
/// </summary>
/// <remarks>
/// <para>
/// No API key, for the reason the mailbox password and the merchant id are not
/// in their own DTOs: it goes in and never comes back out.
/// <see cref="HasApiKey"/> is how the form knows one is stored.
/// </para>
/// <para>
/// The two line settings are not interchangeable and the screen has to say so.
/// <see cref="LineNumber"/> is the advertising line campaigns go out on;
/// <see cref="OtpTemplateId"/> names a template registered on the service line,
/// which is the only thing that reaches a number that has blocked
/// advertising — and so the only thing a sign-in code can use.
/// </para>
/// </remarks>
public sealed record SmsSettingsDto(
    string Provider,
    bool HasApiKey,
    string LineNumber,
    int OtpTemplateId,
    string OtpParameterName);

/// <summary>What the SMS settings screen submits.</summary>
/// <param name="ApiKey">
/// Null leaves the stored one alone; an explicit empty string clears it.
/// </param>
public sealed record SaveSmsSettingsRequest(
    string Provider,
    string? ApiKey,
    string LineNumber,
    int OtpTemplateId,
    string OtpParameterName);

/// <summary>What the settings screen's test button asks for.</summary>
/// <remarks>
/// A real number, because there is no way to prove an SMS account works without
/// one arriving somewhere. The operator's own phone is the intended answer.
/// </remarks>
public sealed record TestSmsRequest(string Phone);
