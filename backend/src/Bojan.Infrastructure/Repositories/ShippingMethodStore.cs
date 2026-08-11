using Bojan.Application.Administration;
using Bojan.Application.Contracts;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Repositories;

/// <summary>
/// The shipping tiers, as the panel edits them.
/// </summary>
/// <remarks>
/// <para>
/// There was no way to change a shipping price. The settings screen wrote three
/// prices into the generic settings table, which nothing read, while the
/// figures the checkout actually charged came from <c>ShippingMethod</c> rows
/// that only the seeder had ever written — so a shop whose courier put its
/// prices up had to be redeployed.
/// </para>
/// <para>
/// Rows are updated, never created or removed. The checkout screens name the
/// three tiers as presentation constants and submit those codes, so a tier that
/// exists here and not there is unreachable, and one removed here is a shopper
/// mid-checkout submitting an id the shop no longer has. Adding a tier is a
/// change to both sides and does not belong behind a settings form.
/// </para>
/// </remarks>
public sealed class ShippingMethodStore(BojanDbContext db) : IShippingMethodStore
{
    public async Task<IReadOnlyList<AdminShippingMethodDto>> ListAsync(CancellationToken cancellationToken) =>
        await db.ShippingMethods.AsNoTracking()
            .OrderBy(method => method.SortOrder)
            .Select(method => new AdminShippingMethodDto(
                method.Code,
                method.Title,
                method.Price.Amount,
                method.Estimate ?? string.Empty,
                method.IsActive,
                method.FreeAboveAmount))
            .ToListAsync(cancellationToken);

    public async Task SaveAsync(
        IReadOnlyList<AdminShippingMethodDto> methods,
        CancellationToken cancellationToken)
    {
        var wanted = methods
            .GroupBy(method => method.Code, StringComparer.Ordinal)
            // Last wins rather than throwing on a repeated code: the screen
            // renders one row per stored tier, so a duplicate can only come
            // from a crafted body, and there is nothing useful to tell its
            // sender.
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        var rows = await db.ShippingMethods
            .Where(method => wanted.Keys.Contains(method.Code))
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            var edit = wanted[row.Code];

            row.Title = edit.Title;
            row.Price = new Domain.Common.Money(edit.Price);
            row.Estimate = edit.Estimate.Length == 0 ? null : edit.Estimate;
            row.IsActive = edit.IsActive;

            // Negative is meaningless — free above minus one is free always,
            // which the operator already has a way to say. Clamped rather than
            // refused, since the screen cannot produce it.
            row.FreeAboveAmount = edit.FreeAboveAmount is { } above ? Math.Max(0, above) : null;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
