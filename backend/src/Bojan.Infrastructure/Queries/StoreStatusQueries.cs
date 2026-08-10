using Bojan.Application.Common;
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
}
