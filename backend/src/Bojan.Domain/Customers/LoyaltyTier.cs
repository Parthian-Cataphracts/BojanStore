using Bojan.Domain.Common;

namespace Bojan.Domain.Customers;

/// <summary>
/// One rung of the loyalty club — the points that reach it and what they buy.
/// </summary>
/// <remarks>
/// <para>
/// The club used to be a page and nothing else. It advertised three tiers, a
/// permanent discount and unlimited free delivery; <c>AddLoyaltyPoints</c> had
/// one caller in the whole codebase and it was the seeder, so no order ever
/// earned a point, no member ever moved a tier, and no discount was ever
/// applied at checkout. Every word of it was a promise to customers that
/// nothing in the system could keep.
/// </para>
/// <para>
/// So the tiers are rows an owner edits, and the two things they grant are the
/// two the shop can actually honour: a standing discount, and delivery at no
/// charge. Nothing here promises what cannot be delivered — the referral and
/// birthday bonuses the page used to advertise are gone, because the shop has
/// no referral feature and nothing that runs on a birthday.
/// </para>
/// </remarks>
public sealed class LoyaltyTier : Entity
{
    public required string Name { get; set; }

    /// <summary>Points a member needs to reach this tier. The first tier is zero.</summary>
    public required int MinimumPoints { get; set; }

    /// <summary>Percent off the goods on every order, for as long as the member holds this tier.</summary>
    public int DiscountPercent { get; set; }

    /// <summary>Whether this tier's members are never charged for delivery.</summary>
    public bool FreeShipping { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// What the club is worth to a member, and what a member earns.
/// </summary>
/// <remarks>
/// A pure function over the tiers, in the domain, because three places have to
/// agree about it — the price a shopper is quoted, the price the order charges,
/// and the tier the account screen shows them. A member told one figure and
/// charged another stops believing the whole programme.
/// </remarks>
public static class Loyalty
{
    /// <summary>The largest standing discount a tier may grant.</summary>
    /// <remarks>
    /// A ceiling on the rule rather than on the form, so a row written straight
    /// into the database cannot sell the shop's stock at a loss. The write path
    /// refuses anything past it too.
    /// </remarks>
    public const int MaxDiscountPercent = 50;

    /// <summary>
    /// The tier a member holds, or null when the club has no tiers configured.
    /// </summary>
    /// <remarks>
    /// The highest rung the member's points reach — floors, like the B2B volume
    /// ladder, so the tiers can be given in any order and still have one answer.
    /// </remarks>
    public static LoyaltyTier? TierFor(IEnumerable<LoyaltyTier> tiers, int points)
    {
        LoyaltyTier? held = null;

        foreach (var tier in tiers)
        {
            if (points >= tier.MinimumPoints &&
                (held is null || tier.MinimumPoints > held.MinimumPoints))
            {
                held = tier;
            }
        }

        return held;
    }

    /// <summary>What a tier takes off the goods on this order.</summary>
    /// <remarks>
    /// Rounded down, so the member never gets less than the percentage promises
    /// and the shop never gives a Toman more than it said it would.
    /// </remarks>
    public static Money DiscountOn(Money goods, LoyaltyTier? tier)
    {
        if (tier is null || tier.DiscountPercent <= 0)
        {
            return Money.Zero;
        }

        var percent = Math.Clamp(tier.DiscountPercent, 0, MaxDiscountPercent);
        return new Money(goods.Amount * percent / 100);
    }

    /// <summary>
    /// Points earned by an order worth <paramref name="goods"/>.
    /// </summary>
    /// <param name="tomanPerPoint">
    /// What a point costs to earn. Zero or less earns nothing, which is how the
    /// owner switches earning off without deleting the tiers.
    /// </param>
    /// <remarks>
    /// Measured on the goods rather than the total, so delivery and the
    /// discounts themselves do not earn points — otherwise a member's own
    /// discount would quietly pay for the next one.
    /// </remarks>
    public static int PointsFor(Money goods, int tomanPerPoint) =>
        tomanPerPoint <= 0 ? 0 : (int)Math.Min(int.MaxValue, goods.Amount / tomanPerPoint);
}
