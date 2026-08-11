using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Notifications;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Notifications;

/// <summary>
/// The shop's Web Push identity, in the same settings table every other section
/// uses.
/// </summary>
/// <remarks>
/// The private key is encrypted rather than hashed, for the reason the SMS key
/// and the mailbox password are: it is used to sign every message, so there is
/// nothing to compare a hash against. Its own protector purpose, so a payload
/// sealed for one section cannot be opened as another.
/// </remarks>
public sealed class WebPushSettingsStore(
    BojanDbContext db,
    IDataProtectionProvider protection,
    IDateTimeProvider clock) : IWebPushSettingsStore
{
    public const string Section = "push";

    private const string ProtectorPurpose = "Bojan.WebPush.PrivateKey.v1";

    private IDataProtector Protector => protection.CreateProtector(ProtectorPurpose);

    public async Task<WebPushSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        Describe(await ReadAsync(cancellationToken));

    /// <summary>The settings plus the decrypted signing key — server-side only.</summary>
    internal async Task<(WebPushSettingsDto Settings, string PrivateKey)> GetWithPrivateKeyAsync(
        CancellationToken cancellationToken)
    {
        var stored = await ReadAsync(cancellationToken);
        var sealedKey = Read(stored, "privateKey");

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
            // as "push is not configured", which points the operator at the one
            // action that fixes it — generating a new pair, which is also the
            // only thing that can be done once the old private key is gone.
            return (Describe(stored), string.Empty);
        }
    }

    public Task SaveAsync(bool enabled, string subject, CancellationToken cancellationToken) =>
        WriteAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["enabled"] = enabled ? "true" : "false",
                ["subject"] = subject.Trim(),
            },
            cancellationToken);

    public async Task<WebPushSettingsDto> GenerateKeysAsync(CancellationToken cancellationToken)
    {
        var (publicKey, privateKey) = WebPushCrypto.GenerateKeyPair();

        await WriteAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["publicKey"] = publicKey,
                ["privateKey"] = Protector.Protect(privateKey),
            },
            cancellationToken);

        return Describe(await ReadAsync(cancellationToken));
    }

    private static WebPushSettingsDto Describe(IReadOnlyDictionary<string, string> stored)
    {
        var publicKey = Read(stored, "publicKey");
        var hasPrivateKey = Read(stored, "privateKey").Length > 0;

        return new WebPushSettingsDto(
            // Switched on is not the same as usable. Half a key pair sends
            // nothing, so the flag reported here is the one the storefront can
            // act on rather than the one that happens to be stored.
            Enabled: Read(stored, "enabled") == "true" && publicKey.Length > 0 && hasPrivateKey,
            PublicKey: publicKey,
            HasPrivateKey: hasPrivateKey,
            Subject: Read(stored, "subject"));
    }

    private async Task WriteAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
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
}
