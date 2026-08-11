using Bojan.Application.Accounts;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Admin;
using Bojan.Domain.Customers;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Repositories;

/// <summary>
/// The loyalty club's tiers and its earning rate.
/// </summary>
/// <remarks>
/// The tiers are rows and the rate is a setting, because they are different
/// shapes of thing — but one screen edits both, so one store writes both and
/// they cannot be saved half-applied.
/// </remarks>
public sealed class LoyaltyStore(BojanDbContext db, IDateTimeProvider clock)
    : ILoyaltyStore, Application.Checkout.ILoyaltySettings
{
    public const string Section = "loyalty";

    /// <summary>
    /// What a shop that has never opened the screen earns at — the figure the
    /// storefront's loyalty page has advertised all along.
    /// </summary>
    private const int DefaultTomanPerPoint = 10_000;

    /// <summary>
    /// Toman a member spends to earn one point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Zero is a real answer and not a missing one: it is how an owner pauses
    /// the club without deleting anyone's balance. Only a value that will not
    /// parse falls back to the default.
    /// </para>
    /// <para>
    /// Both interfaces are implemented here rather than in two stores. They were
    /// two, briefly, and they disagreed about exactly this — one read zero as
    /// "paused" and the other as "unset", so an owner who paused the club had it
    /// quietly keep paying out. One value, one reader.
    /// </para>
    /// </remarks>
    public async Task<int> TomanPerPointAsync(CancellationToken cancellationToken) =>
        Parse(await ReadRateAsync(cancellationToken));

    private static int Parse(string? stored) =>
        int.TryParse(stored, out var rate) && rate >= 0 ? rate : DefaultTomanPerPoint;

    private Task<string?> ReadRateAsync(CancellationToken cancellationToken) =>
        db.Settings.AsNoTracking()
            .Where(entry => entry.Section == Section && entry.Key == "tomanPerPoint")
            .Select(entry => entry.Value)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<LoyaltyProgrammeDto> GetAsync(CancellationToken cancellationToken)
    {
        var tiers = await db.LoyaltyTiers.AsNoTracking()
            .OrderBy(tier => tier.MinimumPoints)
            .Select(tier => new LoyaltyTierDto(
                tier.Name,
                tier.MinimumPoints,
                tier.DiscountPercent,
                tier.FreeShipping,
                tier.SortOrder))
            .ToListAsync(cancellationToken);

        // A club with no tiers is not a club. The storefront hides the page
        // rather than drawing an empty ladder under a heading that promises
        // rewards.
        return new LoyaltyProgrammeDto(
            Enabled: tiers.Count > 0,
            TomanPerPoint: Parse(await ReadRateAsync(cancellationToken)),
            Tiers: tiers);
    }

    /// <remarks>
    /// Replaced wholesale rather than merged, like the shipping and volume-tier
    /// screens: the form shows the whole ladder, so a removed rung has to be a
    /// removed row.
    /// </remarks>
    public async Task SaveAsync(
        int tomanPerPoint,
        IReadOnlyList<LoyaltyTierDto> tiers,
        CancellationToken cancellationToken)
    {
        db.LoyaltyTiers.RemoveRange(await db.LoyaltyTiers.ToListAsync(cancellationToken));

        db.LoyaltyTiers.AddRange(tiers.Select(tier => new LoyaltyTier
        {
            Name = tier.Name,
            MinimumPoints = tier.MinimumPoints,
            DiscountPercent = tier.DiscountPercent,
            FreeShipping = tier.FreeShipping,
            SortOrder = tier.SortOrder,
        }));

        var rate = tomanPerPoint.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var existing = await db.Settings
            .FirstOrDefaultAsync(
                entry => entry.Section == Section && entry.Key == "tomanPerPoint",
                cancellationToken);

        if (existing is null)
        {
            db.Settings.Add(new SettingEntry
            {
                Section = Section,
                Key = "tomanPerPoint",
                Value = rate,
                UpdatedAtUtc = clock.UtcNow,
            });
        }
        else
        {
            existing.Value = rate;
            existing.UpdatedAtUtc = clock.UtcNow;
        }
    }
}
