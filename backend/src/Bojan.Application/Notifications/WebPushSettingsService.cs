using Bojan.Application.Common;
using Bojan.Application.Contracts;

namespace Bojan.Application.Notifications;

/// <summary>
/// The panel's Web Push screen, owner only.
/// </summary>
/// <remarks>
/// Owner rather than a marketing role, for the reason the SMS account and the
/// payment gateway are: whoever holds the private key can send a notification
/// in the shop's name to every browser that ever agreed to hear from it, and
/// replacing the key pair silently disconnects all of them.
/// </remarks>
public sealed class WebPushSettingsService(IWebPushSettingsStore store, IAuditLog audit)
{
    /// <summary>The longest a contact subject may be — a mailto or a URL, not a paragraph.</summary>
    private const int MaxSubjectLength = 200;

    public Task<WebPushSettingsDto> GetAsync(CancellationToken cancellationToken) => store.GetAsync(cancellationToken);

    public async Task<UseCaseResult> SaveAsync(
        SaveWebPushSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var subject = request.Subject.Trim();

        if (subject.Length > MaxSubjectLength)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "subject");
        }

        // RFC 8292 allows only these two forms, and a push service that
        // validates it refuses everything the shop sends until it is fixed — a
        // failure with no symptom except notifications that never arrive. So it
        // is checked here, where the operator is looking at the field.
        if (subject.Length > 0 &&
            !subject.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) &&
            !subject.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "subject");
        }

        // Turning it on without a key pair would report enabled and send
        // nothing. The screen generates keys with its own button, so this is a
        // refusal an operator can act on rather than a state they can reach.
        if (request.Enabled)
        {
            var current = await store.GetAsync(cancellationToken);

            if (current.PublicKey.Length == 0 || !current.HasPrivateKey)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "keys");
            }
        }

        await store.SaveAsync(request.Enabled, subject, cancellationToken);
        audit.Record("push.settings.saved", request.Enabled ? "enabled" : "disabled");

        return UseCaseResult.Success();
    }

    /// <summary>
    /// Mints a new key pair.
    /// </summary>
    /// <remarks>
    /// Audited loudly. Every browser subscribed under the old public key becomes
    /// unreachable the moment this returns — they recorded that key when they
    /// agreed, and a message signed by the new one is from someone else as far
    /// as they are concerned. There is no way to undo it and no way to tell them.
    /// </remarks>
    public async Task<WebPushSettingsDto> GenerateKeysAsync(CancellationToken cancellationToken)
    {
        var settings = await store.GenerateKeysAsync(cancellationToken);
        audit.Record("push.keys.generated", settings.PublicKey);

        return settings;
    }
}
