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
    public async Task<bool> IsMaintenanceModeEnabledAsync(CancellationToken cancellationToken)
    {
        var raw = await db.Settings
            .AsNoTracking()
            .Where(s => s.Section == "general" && s.Key == "maintenance")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        // Stored JSON-encoded ("true"/"false"), same convention as every other
        // settings value — see SettingEntry.Value. No row yet means the switch
        // has never been touched, which reads as off.
        return raw is not null && bool.TryParse(raw, out var enabled) && enabled;
    }
}
