using Bojan.Application.Common;
using Bojan.Application.Contracts;

namespace Bojan.Application.Notifications;

/// <summary>
/// The panel's SMS settings screen, owner only.
/// </summary>
/// <remarks>
/// Owner rather than a support role, for the reason the mailbox settings and
/// the payment gateway are: the API key spends the shop's SMS credit and can
/// send anything in the shop's name to any number.
/// </remarks>
public sealed class SmsSettingsService(
    ISmsSettingsStore store,
    ISmsProbe probe,
    IAuditLog audit,
    IUnitOfWork unitOfWork)
{
    public Task<SmsSettingsDto> GetAsync(CancellationToken cancellationToken) => store.GetAsync(cancellationToken);

    /// <summary>
    /// Saves the screen.
    /// </summary>
    /// <remarks>
    /// A provider turned on without the two things it cannot work without — a
    /// key and a template id — is refused rather than stored. Half-configured
    /// it reads as selected on the screen and silently stops sign-in, which is
    /// the failure this whole screen exists to make visible.
    /// </remarks>
    public async Task<UseCaseResult> SaveAsync(SaveSmsSettingsRequest request, CancellationToken cancellationToken)
    {
        if (!SmsProviders.IsKnown(request.Provider))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "provider");
        }

        var parameterName = request.OtpParameterName.Trim();

        if (request.Provider is SmsProviders.SmsIr)
        {
            var current = await store.GetAsync(cancellationToken);
            var supplied = request.ApiKey?.Trim() ?? string.Empty;

            if (supplied.Length == 0 && !current.HasApiKey)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "apiKey");
            }

            if (request.OtpTemplateId <= 0)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "otpTemplateId");
            }

            // The name has to match the placeholder inside the template
            // registered with the provider. Empty is refused rather than
            // defaulted, because a silent default that does not match is a
            // template that renders with the code missing.
            if (parameterName.Length is 0 or > 50)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "otpParameterName");
            }
        }

        await store.SaveAsync(
            new SmsSettingsDto(
                request.Provider,
                // Not read on the way in — the store keeps whatever is already
                // there when no new value is supplied.
                HasApiKey: false,
                request.LineNumber.Trim(),
                request.OtpTemplateId,
                parameterName),
            request.ApiKey,
            cancellationToken);

        audit.Record("sms.settings.saved", request.Provider);

        // Committed here — see PaymentSettingsService for the whole story. The
        // store saves itself, so a row added after it never reached the table.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult.Success();
    }

    /// <summary>
    /// Sends one real message — the screen's test button.
    /// </summary>
    /// <remarks>
    /// It goes through the verification template on purpose. That is the path
    /// whose breakage is invisible: a template id that does not exist, or a
    /// parameter name that does not match the one in the template, stops every
    /// customer signing in and produces no other symptom.
    /// </remarks>
    public async Task<ProviderTestResult> TestAsync(string phone, CancellationToken cancellationToken)
    {
        if (phone.Trim().Length is 0 or > 20)
        {
            return ProviderTestResult.Fail("شماره موبایل معتبر نیست.");
        }

        var result = await probe.TestAsync(phone.Trim(), cancellationToken);

        // Recorded whichever way it went. A test that sends a real message is a
        // real message, and one operator's failed test explains another's
        // "why did I get an SMS from us".
        audit.Record("sms.settings.tested", result.Ok ? "ok" : "failed");

        // Nothing else in this method writes, so without this the entry the
        // comment above calls for would be discarded when the scope ended.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return result;
    }
}
