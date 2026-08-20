using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Support;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Common;
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
/// <summary>
/// Whether outbound mail is set up, field by field.
/// </summary>
/// <remarks>
/// Not a single boolean, because "it is not configured" is the answer an owner
/// already has — what they need is which of the four things is missing.
/// </remarks>
public readonly record struct MailboxSendReadiness(
    bool Enabled,
    bool HasSmtpHost,
    bool HasSenderAddress,
    bool HasPassword)
{
    /// <summary>The condition <c>SmtpEmailSender</c> requires before it will send.</summary>
    public bool IsReady => Enabled && HasSmtpHost && HasSenderAddress && HasPassword;
}

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
        return Describe(stored, Protector.UnprotectOrEmpty(Read(stored, "password")).Length > 0);
    }

    /// <summary>
    /// <paramref name="hasPassword"/> is passed in rather than measured off
    /// <paramref name="stored"/>: the question the form is asking is whether a
    /// usable password is on file, and one sealed by a key ring that is gone
    /// is not. See <see cref="ProtectedSecret"/>.
    /// </summary>
    private static MailboxSettingsDto Describe(IReadOnlyDictionary<string, string> stored, bool hasPassword)
    {
        return new MailboxSettingsDto(
            Enabled: Read(stored, "enabled") == "true",
            ImapHost: Read(stored, "imapHost"),
            ImapPort: ReadInt(stored, "imapPort", 993),
            ImapUseSsl: Read(stored, "imapUseSsl") != "false",
            SmtpHost: Read(stored, "smtpHost"),
            SmtpPort: ReadInt(stored, "smtpPort", 587),
            SmtpUseSsl: Read(stored, "smtpUseSsl") != "false",
            Username: Read(stored, "username"),
            HasPassword: hasPassword,
            Address: Read(stored, "address"),
            DisplayName: Read(stored, "displayName") is { Length: > 0 } name ? name : "پشتیبانی بوژان");
    }

    /// <summary>
    /// Whether a message posted right now would actually leave the building,
    /// and which field is missing when it would not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same four conditions <c>SmtpEmailSender</c> checks before falling
    /// back to writing the message to the log, exposed so the health board can
    /// say so out loud. It carries no secret: four booleans and nothing else.
    /// </para>
    /// <para>
    /// This screen was once the only one that asked whether the password could
    /// actually be read back, because <see cref="MailboxSettingsDto.HasPassword"/>
    /// then meant no more than "a value is stored" — and a key ring that was
    /// rotated or lost leaves a stored password that opens to nothing. That is
    /// no longer a distinction: the flag is now decided the same way, here and
    /// on every other settings screen. See <see cref="ProtectedSecret"/>.
    /// </para>
    /// </remarks>
    public async Task<MailboxSendReadiness> GetSendReadinessAsync(CancellationToken cancellationToken)
    {
        var (settings, password) = await GetWithPasswordAsync(cancellationToken);

        return new MailboxSendReadiness(
            settings.Enabled,
            settings.SmtpHost.Length > 0,
            settings.Address.Length > 0,
            password.Length > 0);
    }

    /// <summary>The settings plus the decrypted password — server-side only.</summary>
    internal async Task<(MailboxSettingsDto Settings, string Password)> GetWithPasswordAsync(
        CancellationToken cancellationToken)
    {
        var stored = await ReadAsync(cancellationToken);
        var password = Protector.UnprotectOrEmpty(Read(stored, "password"));

        return (Describe(stored, password.Length > 0), password);
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
