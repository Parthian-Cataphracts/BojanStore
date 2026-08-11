using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Queries;

/// <summary>
/// Reads the one setting a visitor is allowed to ask about without a
/// credential — see <see cref="IStoreStatusQueries"/>.
/// </summary>
public sealed class StoreStatusQueries(BojanDbContext db) : IStoreStatusQueries
{
    /// <summary>
    /// Reads <c>store/maintenance</c> — the row the panel's switch actually
    /// writes.
    /// </summary>
    /// <remarks>
    /// It used to read <c>general/maintenance</c>, and nothing has ever
    /// written a <c>general</c> section: the switch sits on the store settings
    /// screen, which posts <c>section="store"</c>. So the operator turned
    /// maintenance mode on, the panel saved it and showed it on, and the
    /// storefront went on serving the shop — the switch was connected to
    /// nothing at either end of its own name.
    /// </remarks>
    public async Task<bool> IsMaintenanceModeEnabledAsync(CancellationToken cancellationToken)
    {
        var raw = await db.Settings
            .AsNoTracking()
            .Where(s => s.Section == "store" && s.Key == "maintenance")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        // The switch posts a bare "true"/"false" — see SettingsForm's hidden
        // input. No row yet means it has never been touched, which reads as off.
        return raw is not null && bool.TryParse(raw, out var enabled) && enabled;
    }

    /// <summary>
    /// The whole <c>store</c> section, shaped for the storefront.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One read of one indexed section rather than a query per field: this is
    /// rendered on every page of the shop, and the endpoint in front of it is
    /// cached, so the cost is one row scan per cache miss.
    /// </para>
    /// <para>
    /// Every field falls back to what the storefront used to have written into
    /// it. A shop that has never opened the settings screen looks exactly as it
    /// did before, and one that has looks the way the owner said — which is the
    /// difference between a configurable shop and a shop that starts empty.
    /// </para>
    /// </remarks>
    public async Task<StorefrontSettingsDto> GetStorefrontSettingsAsync(CancellationToken cancellationToken)
    {
        var stored = await db.Settings.AsNoTracking()
            .Where(entry => entry.Section == "store")
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Value, cancellationToken);

        string Read(string key, string fallback = "")
        {
            var value = stored.TryGetValue(key, out var raw) ? raw.Trim() : string.Empty;
            return value.Length > 0 ? value : fallback;
        }

        long ReadAmount(string key, long fallback)
        {
            var value = Read(key);
            return long.TryParse(value, out var amount) && amount >= 0 ? amount : fallback;
        }

        int ReadCount(string key, int fallback)
        {
            var value = Read(key);
            return int.TryParse(value, out var count) && count >= 0 ? count : fallback;
        }

        var email = Read("email");

        return new StorefrontSettingsDto(
            new StoreIdentityDto(
                Read("storeName", "بوژان"),
                Read("tagline", "برای لحظه‌های خلاق زندگی"),
                Read("description")),
            new StoreContactDto(
                Read("phone"),
                email,
                Read("businessPhone"),
                // A shop without a separate organisational address answers B2B
                // enquiries on its main one, which is what happens in practice —
                // better than showing a row that is blank.
                Read("businessEmail", email),
                Read("address"),
                Read("postalCode"),
                Read("workingHours")),
            new StoreSocialDto(
                Read("instagram"),
                Read("telegram"),
                Read("whatsapp"),
                Read("linkedin")),
            new StorePromisesDto(
                ReadAmount("freeShippingThreshold", 1_000_000),
                ReadCount("returnWindowDays", 7),
                Read("deliveryEstimate", "۲ تا ۵ روز کاری"),
                Read("supportPromise")));
    }
}
