using Bojan.Domain.Common;
using Bojan.Domain.Customers;

namespace Bojan.Domain.Tests;

/// <summary>
/// What the loyalty club is worth, and what it earns.
/// </summary>
/// <remarks>
/// Three places have to agree about these figures — the price a shopper is
/// quoted, the price the order charges, and the tier their account screen shows
/// them. A member told one number and charged another stops believing the
/// programme, so the arithmetic is a pure function and this is where it is
/// pinned.
/// </remarks>
public sealed class LoyaltyTests
{
    private static LoyaltyTier Tier(string name, int minimum, int percent, bool freeShipping = false) => new()
    {
        Name = name,
        MinimumPoints = minimum,
        DiscountPercent = percent,
        FreeShipping = freeShipping,
    };

    private static readonly LoyaltyTier[] Tiers =
    [
        Tier("برنزی", 0, 0),
        Tier("نقره‌ای", 1_000, 5),
        Tier("طلایی", 3_000, 10, freeShipping: true),
    ];

    [Theory]
    [InlineData(0, "برنزی")]
    [InlineData(999, "برنزی")]
    [InlineData(1_000, "نقره‌ای")]
    [InlineData(2_999, "نقره‌ای")]
    [InlineData(3_000, "طلایی")]
    [InlineData(50_000, "طلایی")]
    public void The_highest_tier_the_points_reach_is_the_one_held(int points, string expected) =>
        Assert.Equal(expected, Loyalty.TierFor(Tiers, points)!.Name);

    /// <summary>Floors, like the B2B ladder, so the rows can be stored in any order.</summary>
    [Fact]
    public void Order_does_not_change_the_answer()
    {
        LoyaltyTier[] shuffled = [Tier("طلایی", 3_000, 10), Tier("برنزی", 0, 0), Tier("نقره‌ای", 1_000, 5)];

        Assert.Equal("نقره‌ای", Loyalty.TierFor(shuffled, 1_500)!.Name);
    }

    /// <summary>
    /// A shop that has configured no tiers has no club. Null rather than an
    /// invented tier, so the page can say nothing instead of promising nothing.
    /// </summary>
    [Fact]
    public void No_tiers_means_no_tier()
    {
        Assert.Null(Loyalty.TierFor([], 10_000));
        Assert.Equal(Money.Zero, Loyalty.DiscountOn(new Money(500_000), null));
    }

    /// <summary>
    /// A member below the first rung. Only possible when the lowest tier asks
    /// for points, which is a shop that makes people earn their way in.
    /// </summary>
    [Fact]
    public void A_member_below_every_rung_holds_no_tier()
    {
        LoyaltyTier[] earned = [Tier("نقره‌ای", 1_000, 5)];

        Assert.Null(Loyalty.TierFor(earned, 999));
    }

    [Fact]
    public void The_discount_is_the_tiers_percent_off_the_goods()
    {
        Assert.Equal(new Money(50_000), Loyalty.DiscountOn(new Money(1_000_000), Tiers[1]));
        Assert.Equal(new Money(100_000), Loyalty.DiscountOn(new Money(1_000_000), Tiers[2]));
        Assert.Equal(Money.Zero, Loyalty.DiscountOn(new Money(1_000_000), Tiers[0]));
    }

    /// <summary>
    /// Rounded down, so the member never gets less than the percentage promises
    /// and the shop never gives a Toman more than it said it would.
    /// </summary>
    [Fact]
    public void A_discount_that_does_not_divide_evenly_rounds_down()
    {
        // 5% of 12,345 is 617.25.
        Assert.Equal(new Money(617), Loyalty.DiscountOn(new Money(12_345), Tiers[1]));
    }

    /// <summary>
    /// A row written straight into the database cannot sell the stock at a loss.
    /// The write path refuses these too; this is the reading that holds if one
    /// ever gets past it.
    /// </summary>
    [Fact]
    public void A_discount_beyond_the_ceiling_is_clamped()
    {
        var absurd = Tier("طمع‌کار", 0, 400);

        Assert.Equal(new Money(500_000), Loyalty.DiscountOn(new Money(1_000_000), absurd));
    }

    [Fact]
    public void A_negative_discount_never_adds_to_the_price()
    {
        var wrong = Tier("اشتباه", 0, -20);

        Assert.Equal(Money.Zero, Loyalty.DiscountOn(new Money(1_000_000), wrong));
    }

    // --- earning -------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9_999, 0)]
    [InlineData(10_000, 1)]
    [InlineData(1_000_000, 100)]
    [InlineData(1_009_999, 100)]
    public void One_point_per_ten_thousand_Toman(long goods, int expected) =>
        Assert.Equal(expected, Loyalty.PointsFor(new Money(goods), 10_000));

    /// <summary>
    /// How the owner switches earning off without deleting the tiers — members
    /// keep what they have and stop accruing.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_rate_of_zero_or_less_earns_nothing(int tomanPerPoint) =>
        Assert.Equal(0, Loyalty.PointsFor(new Money(5_000_000), tomanPerPoint));

    /// <summary>
    /// An order large enough to overflow the counter is not a reason to throw at
    /// the customer — it is a reason to stop counting.
    /// </summary>
    [Fact]
    public void An_absurd_order_does_not_overflow_the_balance()
    {
        Assert.Equal(int.MaxValue, Loyalty.PointsFor(new Money(long.MaxValue), 1));
    }
}
