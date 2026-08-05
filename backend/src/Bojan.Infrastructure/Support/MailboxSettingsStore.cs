using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Support;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Support;

/// <summary>
/// The mailbox connection settings, in the same settings table every other
/// section uses.
/// </summary>
/// <remarks>
/// <para>
/// The password is the odd one out in this codebase. Every other secret here is
/// hashed and never recovered — an API key, an operator's password, an OTP —
/// because nothing needs the original back. This one has to be replayed to an
/// IMAP server on every connection, so it is encrypted rather than hashed, with
/// ASP.NET Core's data protection: the key ring lives outside the database, so
/// a dumped table alone does not yield the credential.
/// </para>
/// <para>
/// It never travels outwards. <see cref="MailboxSettingsDto"/> has no password
/// field at all, so there is no route by which the panel could render it —
/// <see cref="MailboxSettingsDto.HasPassword"/> is how the form knows one is
/// stored.
/// </para>
/// </remarks>
public sealed class MailboxSettingsStore(BojanDbContext db, IDataProtectionProvider protection, IDateTimeProvider clock)
    : IMailboxSettingsStore
{
    /// <summary>The settings section these live under.</summary>
    public const string Section = "mailbox";

    /// <summary>
    /// Names the purpose of the key.
    /// </summary>
    /// <remarks>
    /// Data protection derives a distinct key per purpose string, so a payload
    /// encrypted for something else in this application cannot be decrypted
    /// here and vice versa.
    /// </remarks>
    private const string ProtectorPurpose = "Bojan.Mailbox.Password.v1";

    private IDataProtector Protector => protection.CreateProtector(ProtectorPurpose);

    public async Task<MailboxSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var stored = await ReadAsync(cancellationToken);

        return new MailboxSettingsDto(
            Enabled: Read(stored, "enabled") == "true",
            ImapHost: Read(stored, "imapHost"),
            ImapPort: ReadInt(stored, "imapPort", 993),
            ImapUseSsl: Read(stored, "imapUseSsl") != "false",
            SmtpHost: Read(stored, "smtpHost"),
            SmtpPort: ReadInt(stored, "smtpPort", 587),
            SmtpUseSsl: Read(stored, "smtpUseSsl") != "false",
            Username: Read(stored, "username"),
            HasPassword: Read(stored, "password").Length > 0,
            Address: Read(stored, "address"),
            DisplayName: Read(stored, "displayName") is { Length: > 0 } name ? name : "پشتیبانی بوژان");
    }

    /// <summary>The settings plus the decrypted password — server-side only.</summary>
    internal async Task<(MailboxSettingsDto Settings, string Password)> GetWithPasswordAsync(
        CancellationToken cancellationToken)
    {
        var settings = await GetAsync(cancellationToken);
        var stored = await ReadAsync(cancellationToken);
        var sealedPassword = Read(stored, "password");

        if (sealedPassword.Length == 0)
        {
            return (settings, string.Empty);
        }

        try
        {
            return (settings, Protector.Unprotect(sealedPassword));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // The key ring was rotated or lost, so the stored value can no
            // longer be read. Empty makes the connection fail with "the
            // password was not accepted", which points the operator at the one
            // action that fixes it — entering it again.
            return (settings, string.Empty);
        }
    }

    public async Task SaveAsync(MailboxSettingsDto settings, string? password, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["enabled"] = settings.Enabled ? "true" : "false",
            ["imapHost"] = settings.ImapHost.Trim(),
            ["imapPort"] = settings.ImapPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["imapUseSsl"] = settings.ImapUseSsl ? "true" : "false",
            ["smtpHost"] = settings.SmtpHost.Trim(),
            ["smtpPort"] = settings.SmtpPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["smtpUseSsl"] = settings.SmtpUseSsl ? "true" : "false",
            ["username"] = settings.Username.Trim(),
            ["address"] = settings.Address.Trim(),
            ["displayName"] = settings.DisplayName.Trim(),
        };

        // Null means "leave it alone", which is what an empty box on a form
        // that never shows the password has to mean. An explicit empty string
        // is how it is cleared.
        if (password is not null)
        {
            values["password"] = password.Length == 0 ? string.Empty : Protector.Protect(password);
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

    private Task<Dictionary<string, string>> ReadAsync(CancellationToken cancellationToken) =>
        db.Settings.AsNoTracking()
            .Where(entry => entry.Section == Section)
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Value, cancellationToken);

    private static string Read(IReadOnlyDictionary<string, string> stored, string key) =>
        stored.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static int ReadInt(IReadOnlyDictionary<string, string> stored, string key, int fallback) =>
        int.TryParse(Read(stored, key), out var value) && value is > 0 and <= 65535 ? value : fallback;
}
