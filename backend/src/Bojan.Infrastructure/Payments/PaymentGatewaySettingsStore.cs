using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Payments;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Common;
using Bojan.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Payments;

/// <summary>
/// Which gateway the shop is pointed at, in the same settings table every other
/// section uses.
/// </summary>
/// <remarks>
/// <para>
/// The merchant id is encrypted rather than hashed, for the reason the mailbox
/// password is: it has to be replayed to ZarinPal on every payment, so there is
/// nothing to compare a hash against. Data protection keeps the key ring
/// outside the database, so a dumped table on its own does not yield the
/// credential.
/// </para>
/// <para>
/// It never travels outwards. <see cref="PaymentGatewaySettingsDto"/> has no
/// field for it, so there is no route by which the panel could render it —
/// <see cref="PaymentGatewaySettingsDto.HasMerchantId"/> is how the form knows
/// one is stored.
/// </para>
/// </remarks>
public sealed class PaymentGatewaySettingsStore(
    BojanDbContext db,
    IDataProtectionProvider protection,
    IDateTimeProvider clock) : IPaymentGatewaySettingsStore
{
    /// <summary>
    /// The settings section these live under.
    /// </summary>
    /// <remarks>
    /// The same section the panel's payment screen already writes, so an
    /// existing deployment's rows stay where they are. The keys are new: the
    /// old screen wrote <c>gateway</c> and <c>merchantId</c> as display text
    /// that nothing read.
    /// </remarks>
    public const string Section = "payment";

    /// <summary>
    /// Names the purpose of the key.
    /// </summary>
    /// <remarks>
    /// Distinct from the mailbox's purpose string, so a payload encrypted for
    /// one cannot be decrypted as the other.
    /// </remarks>
    private const string ProtectorPurpose = "Bojan.Payment.MerchantId.v1";

    private IDataProtector Protector => protection.CreateProtector(ProtectorPurpose);

    public async Task<PaymentGatewaySettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var stored = await ReadAsync(cancellationToken);
        return Describe(stored, Protector.UnprotectOrEmpty(Read(stored, "merchantId")).Length > 0);
    }

    /// <summary>The settings plus the decrypted merchant id — server-side only.</summary>
    internal async Task<(PaymentGatewaySettingsDto Settings, string MerchantId)> GetWithMerchantIdAsync(
        CancellationToken cancellationToken)
    {
        var stored = await ReadAsync(cancellationToken);
        var merchantId = Protector.UnprotectOrEmpty(Read(stored, "merchantId"));

        return (Describe(stored, merchantId.Length > 0), merchantId);
    }

    public async Task SaveAsync(
        PaymentGatewaySettingsDto settings,
        string? merchantId,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["provider"] = PaymentProviders.IsKnown(settings.Provider) ? settings.Provider : PaymentProviders.None,
            ["sandbox"] = settings.UseSandboxEndpoints ? "true" : "false",
            ["callbackUrl"] = settings.CallbackUrl.Trim(),
            ["description"] = settings.Description.Trim(),
        };

        // Null means "leave it alone", which is what an empty box on a form
        // that never shows the credential has to mean. An explicit empty string
        // is how it is cleared.
        if (merchantId is not null)
        {
            var trimmed = merchantId.Trim();
            values["merchantId"] = trimmed.Length == 0 ? string.Empty : Protector.Protect(trimmed);
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

    /// <summary>
    /// <paramref name="hasMerchantId"/> is passed in rather than measured off
    /// <paramref name="stored"/>: a merchant id sealed by a key ring that is
    /// gone cannot be sent to the gateway, whatever the row holds. See
    /// <see cref="ProtectedSecret"/>.
    /// </summary>
    private static PaymentGatewaySettingsDto Describe(
        IReadOnlyDictionary<string, string> stored, bool hasMerchantId)
    {
        var provider = Read(stored, "provider");

        return new PaymentGatewaySettingsDto(
            Provider: PaymentProviders.IsKnown(provider) ? provider : PaymentProviders.None,
            UseSandboxEndpoints: Read(stored, "sandbox") == "true",
            HasMerchantId: hasMerchantId,
            CallbackUrl: Read(stored, "callbackUrl"),
            Description: Read(stored, "description"));
    }

    private Task<Dictionary<string, string>> ReadAsync(CancellationToken cancellationToken) =>
        db.Settings.AsNoTracking()
            .Where(entry => entry.Section == Section)
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Value, cancellationToken);

    private static string Read(IReadOnlyDictionary<string, string> stored, string key) =>
        stored.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
}
