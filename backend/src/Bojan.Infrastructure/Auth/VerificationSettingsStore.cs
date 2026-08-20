using Bojan.Application.Auth;
using Bojan.Application.Common;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Auth;

/// <summary>
/// The two verification toggles, in the same settings table every other
/// section uses.
/// </summary>
/// <remarks>
/// Simpler than <c>SmsSettingsStore</c> or <c>MailboxSettingsStore</c>: there
/// is no secret here to seal with <c>IDataProtectionProvider</c>, just two
/// booleans an owner flips.
/// </remarks>
public sealed class VerificationSettingsStore(BojanDbContext db, IDateTimeProvider clock) : IVerificationSettingsStore
{
    public const string Section = "verification";

    public async Task<VerificationSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var stored = await db.Settings.AsNoTracking()
            .Where(entry => entry.Section == Section)
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Value, cancellationToken);

        return new VerificationSettingsDto(
            RequireEmailVerification: Read(stored, "requireEmailVerification"),
            RequirePhoneVerification: Read(stored, "requirePhoneVerification"));
    }

    public async Task SaveAsync(VerificationSettingsDto settings, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["requireEmailVerification"] = settings.RequireEmailVerification.ToString(),
            ["requirePhoneVerification"] = settings.RequirePhoneVerification.ToString(),
        };

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
    }

    private static bool Read(IReadOnlyDictionary<string, string> stored, string key) =>
        stored.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) && parsed;
}
