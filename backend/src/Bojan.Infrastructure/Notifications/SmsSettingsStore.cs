using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Notifications;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Notifications;

/// <summary>
/// The SMS account, in the same settings table every other section uses.
/// </summary>
/// <remarks>
/// The API key is encrypted rather than hashed, for the reason the mailbox
/// password and the payment merchant id are: it is replayed to the provider on
/// every message, so there is nothing to compare a hash against. Its own
/// protector purpose, so a payload sealed for one of the three cannot be opened
/// as another.
/// </remarks>
public sealed class SmsSettingsStore(
    BojanDbContext db,
    IDataProtectionProvider protection,
    IDateTimeProvider clock) : ISmsSettingsStore
{
    public const string Section = "sms";

    private const string ProtectorPurpose = "Bojan.Sms.ApiKey.v1";

    /// <summary>
    /// The template placeholder a fresh install assumes.
    /// </summary>
    /// <remarks>
    /// SMS.ir's own documentation and every sample they publish use this name,
    /// so it is the one an operator who has just created a template will have.
    /// It is still editable, because the name is theirs to choose.
    /// </remarks>
    private const string DefaultParameterName = "Code";

    private IDataProtector Protector => protection.CreateProtector(ProtectorPurpose);

    public async Task<SmsSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        Describe(await ReadAsync(cancellationToken));

    /// <summary>The settings plus the decrypted key — server-side only.</summary>
    internal async Task<(SmsSettingsDto Settings, string ApiKey)> GetWithApiKeyAsync(
        CancellationToken cancellationToken)
    {
        var stored = await ReadAsync(cancellationToken);
        var sealedKey = Read(stored, "apiKey");

        if (sealedKey.Length == 0)
        {
            return (Describe(stored), string.Empty);
        }

        try
        {
            return (Describe(stored), Protector.Unprotect(sealedKey));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // The key ring was rotated or lost. Empty makes the next send fail
            // as "no API key is configured", which points the operator at the
            // one action that fixes it.
            return (Describe(stored), string.Empty);
        }
    }

    public async Task SaveAsync(SmsSettingsDto settings, string? apiKey, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["provider"] = SmsProviders.IsKnown(settings.Provider) ? settings.Provider : SmsProviders.None,
            ["lineNumber"] = settings.LineNumber.Trim(),
            ["otpTemplateId"] = settings.OtpTemplateId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["otpParameterName"] = settings.OtpParameterName.Trim(),
        };

        if (apiKey is not null)
        {
            var trimmed = apiKey.Trim();
            values["apiKey"] = trimmed.Length == 0 ? string.Empty : Protector.Protect(trimmed);
        }

        var existing = await db.Settings
            .Where(entry => entry.Section == Section)
            .ToDictionaryAsync(entry => entry.Key, cancellationToken);

        foreach (var (key, value) in values)
        {
            if (existing.TryGetValue(key, out var entry))
            {
                entry.Value = value;
                entry.UpdatedAtUtc = clock.UtcNow;
            }
            else
            {
                db.Settings.Add(new SettingEntry
                {
                    Section = Section,
                    Key = key,
                    Value = value,
                    UpdatedAtUtc = clock.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static SmsSettingsDto Describe(IReadOnlyDictionary<string, string> stored)
    {
        var provider = Read(stored, "provider");
        var parameterName = Read(stored, "otpParameterName");

        return new SmsSettingsDto(
            Provider: SmsProviders.IsKnown(provider) ? provider : SmsProviders.None,
            HasApiKey: Read(stored, "apiKey").Length > 0,
            LineNumber: Read(stored, "lineNumber"),
            OtpTemplateId: int.TryParse(Read(stored, "otpTemplateId"), out var template) && template > 0 ? template : 0,
            OtpParameterName: parameterName.Length > 0 ? parameterName : DefaultParameterName);
    }

    private Task<Dictionary<string, string>> ReadAsync(CancellationToken cancellationToken) =>
        db.Settings.AsNoTracking()
            .Where(entry => entry.Section == Section)
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Value, cancellationToken);

    private static string Read(IReadOnlyDictionary<string, string> stored, string key) =>
        stored.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
}
