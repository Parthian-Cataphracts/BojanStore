using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;

namespace Bojan.Domain.Tests;

/// <summary>
/// What a quantity costs once the volume ladder is applied.
/// </summary>
/// <remarks>
/// Two places have to agree about this — the pro-forma a rep issues and the
/// ladder an organisation is shown before asking — and a buyer quoted one figure
/// and shown another stops trusting both. So it is a pure function in the
/// domain, and this is where it is pinned.
/// </remarks>
public sealed class VolumePricingTests
{
    private static ProductVolumeTier Tier(int minimum, int percent) => new()
    {
        ProductId = Guid.NewGuid(),
        MinimumQuantity = minimum,
        DiscountPercent = percent,
    };

    private static readonly ProductVolumeTier[] Ladder =
    [
        Tier(20, 10),
        Tier(100, 18),
        Tier(500, 25),
    ];

    [Theory]
    [InlineData(1, 0)]
    [InlineData(19, 0)]
    [InlineData(20, 10)]
    [InlineData(99, 10)]
    [InlineData(100, 18)]
    [InlineData(499, 18)]
    [InlineData(500, 25)]
    [InlineData(10_000, 25)]
    public void The_highest_rung_the_quantity_reaches_is_the_one_that_applies(int quantity, int expected) =>
        Assert.Equal(expected, VolumePricing.DiscountPercentFor(Ladder, quantity));

    /// <summary>
    /// Floors rather than ranges, so the ladder can be given in any order and
    /// still have one answer — an operator typing rows is not sorting them.
    /// </summary>
    [Fact]
    public void Order_does_not_change_the_answer()
    {
        ProductVolumeTier[] shuffled = [Tier(500, 25), Tier(20, 10), Tier(100, 18)];

        Assert.Equal(18, VolumePricing.DiscountPercentFor(shuffled, 120));
    }

    [Fact]
    public void A_product_with_no_ladder_is_sold_at_its_list_price()
    {
        Assert.Equal(0, VolumePricing.DiscountPercentFor([], 1_000));
        Assert.Equal(new Money(50_000), VolumePricing.UnitPriceFor(new Money(50_000), [], 1_000));
    }

    [Fact]
    public void The_unit_price_is_the_list_price_less_the_rung()
    {
        Assert.Equal(new Money(45_000), VolumePricing.UnitPriceFor(new Money(50_000), Ladder, 20));
        Assert.Equal(new Money(41_000), VolumePricing.UnitPriceFor(new Money(50_000), Ladder, 100));
        Assert.Equal(new Money(50_000), VolumePricing.UnitPriceFor(new Money(50_000), Ladder, 19));
    }

    /// <summary>
    /// Rounded down, so the discount is never less than the ladder promises and
    /// the shop never quotes a rial more than the percentage says.
    /// </summary>
    [Fact]
    public void A_price_that_does_not_divide_evenly_rounds_in_the_buyers_favour()
    {
        // 10% off 12,345 is 11,110.5.
        Assert.Equal(new Money(11_110), VolumePricing.UnitPriceFor(new Money(12_345), Ladder, 20));
    }

    /// <summary>
    /// Two rungs sharing a floor is a contradiction somebody typed. The write
    /// path refuses it, and this is the reading that does not surprise a buyer
    /// if one ever reaches the pricing anyway.
    /// </summary>
    [Fact]
    public void Two_rungs_at_one_floor_resolve_to_the_larger_discount()
    {
        ProductVolumeTier[] contradictory = [Tier(20, 10), Tier(20, 15)];

        Assert.Equal(15, VolumePricing.DiscountPercentFor(contradictory, 30));
    }

    [Fact]
    public void A_discount_beyond_a_hundred_percent_cannot_make_the_shop_pay()
    {
        ProductVolumeTier[] nonsense = [Tier(2, 500)];

        Assert.Equal(100, VolumePricing.DiscountPercentFor(nonsense, 10));
        Assert.Equal(Money.Zero, VolumePricing.UnitPriceFor(new Money(50_000), nonsense, 10));
    }
}
