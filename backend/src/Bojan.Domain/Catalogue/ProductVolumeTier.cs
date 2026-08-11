using Bojan.Domain.Common;

namespace Bojan.Domain.Catalogue;

/// <summary>
/// A quantity break on one product — "from twenty units, ten percent off".
/// </summary>
/// <remarks>
/// <para>
/// Organisations do not buy at the shelf price, and the discount they get is a
/// function of how many they take. Writing that as one percentage per customer
/// would be the wrong shape: the same organisation buying two of something and
/// two hundred of it has not earned the same terms, and a rep quoting by hand
/// gets it inconsistently right.
/// </para>
/// <para>
/// Tiers belong to the product rather than to the buyer, because what makes a
/// hundred units cheaper is the product's own economics — one carton, one
/// picking run, one delivery. A particular organisation negotiating something
/// better than the published ladder is a discount on the quote, which is a
/// separate field a rep sets deliberately.
/// </para>
/// <para>
/// This is B2B pricing only. Nothing on the storefront's retail path reads it:
/// a shopper buying twenty pencils is not an organisation, and quietly repricing
/// a retail basket by volume is a different commercial decision that nobody has
/// made.
/// </para>
/// </remarks>
public sealed class ProductVolumeTier : Entity
{
    public required Guid ProductId { get; init; }

    /// <summary>
    /// The quantity at which this tier starts applying.
    /// </summary>
    /// <remarks>
    /// The ladder is expressed as floors rather than ranges — 20, 100, 500 —
    /// because ranges have to be kept adjacent by hand and a gap between two of
    /// them is a quantity with no price. A floor cannot leave a gap.
    /// </remarks>
    public required int MinimumQuantity { get; set; }

    /// <summary>Whole percent off the product's list price at this quantity.</summary>
    public required int DiscountPercent { get; set; }
}

/// <summary>
/// What a quantity costs once the ladder is applied.
/// </summary>
/// <remarks>
/// In the domain, and pure, because two places have to agree about it: the
/// quote a rep issues and the ladder the organisation is shown before asking.
/// A buyer quoted one figure and shown another stops trusting both.
/// </remarks>
public static class VolumePricing
{
    /// <summary>
    /// The percentage off at this quantity — the highest tier it reaches.
    /// </summary>
    /// <remarks>
    /// Highest floor at or below the quantity wins, so tiers can be listed in
    /// any order and an overlapping ladder still has one answer. Zero when the
    /// quantity reaches no tier, which is also the answer for a product with no
    /// ladder at all.
    /// </remarks>
    public static int DiscountPercentFor(IEnumerable<ProductVolumeTier> tiers, int quantity)
    {
        var best = 0;
        var floor = 0;

        foreach (var tier in tiers)
        {
            if (tier.MinimumQuantity > quantity) continue;

            // `>=` rather than `>`: two tiers sharing a floor is a
            // contradiction somebody typed, and taking the larger discount is
            // the reading that does not surprise the buyer.
            if (tier.MinimumQuantity > floor || (tier.MinimumQuantity == floor && tier.DiscountPercent > best))
            {
                floor = tier.MinimumQuantity;
                best = tier.DiscountPercent;
            }
        }

        return Math.Clamp(best, 0, 100);
    }

    /// <summary>
    /// The unit price at this quantity.
    /// </summary>
    /// <remarks>
    /// Rounded down, so the discount is never less than the ladder promises and
    /// the shop never quotes a rial more than the percentage says.
    /// </remarks>
    public static Money UnitPriceFor(Money listPrice, IEnumerable<ProductVolumeTier> tiers, int quantity)
    {
        var percent = DiscountPercentFor(tiers, quantity);
        if (percent == 0) return listPrice;

        return new Money(listPrice.Amount * (100 - percent) / 100);
    }
}
