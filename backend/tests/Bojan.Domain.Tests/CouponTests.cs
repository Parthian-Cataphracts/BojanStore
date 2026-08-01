using Bojan.Domain.Common;
using Bojan.Domain.Orders;

namespace Bojan.Domain.Tests;

public class CouponTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Percent_discount_is_computed_from_subtotal()
    {
        var coupon = new Coupon { Code = "TEN", PercentOff = 10 };

        var discount = coupon.Validate(new Money(1_000_000), Now);

        Assert.Equal(new Money(100_000), discount);
    }

    [Fact]
    public void Fixed_discount_never_exceeds_the_subtotal()
    {
        var coupon = new Coupon { Code = "BIG", AmountOff = new Money(500_000) };

        var discount = coupon.Validate(new Money(100_000), Now);

        Assert.Equal(new Money(100_000), discount);
    }

    [Fact]
    public void Expired_coupon_is_rejected()
    {
        var coupon = new Coupon { Code = "OLD", PercentOff = 10, ExpiresAtUtc = Now.AddDays(-1) };

        Assert.Throws<InvalidOperationException>(() => coupon.Validate(new Money(100_000), Now));
    }

    [Fact]
    public void Inactive_coupon_is_rejected()
    {
        var coupon = new Coupon { Code = "OFF", PercentOff = 10, IsActive = false };

        Assert.Throws<InvalidOperationException>(() => coupon.Validate(new Money(100_000), Now));
    }

    [Fact]
    public void Below_minimum_spend_is_rejected()
    {
        var coupon = new Coupon { Code = "MIN", PercentOff = 10, MinimumSpend = new Money(200_000) };

        Assert.Throws<InvalidOperationException>(() => coupon.Validate(new Money(100_000), Now));
    }

    [Fact]
    public void Exhausted_redemptions_are_rejected()
    {
        var coupon = new Coupon { Code = "ONCE", PercentOff = 10, MaxRedemptions = 1 };
        coupon.RecordRedemption();

        Assert.Throws<InvalidOperationException>(() => coupon.Validate(new Money(100_000), Now));
    }

    [Fact]
    public void At_minimum_spend_exactly_is_accepted()
    {
        var coupon = new Coupon { Code = "MIN", PercentOff = 10, MinimumSpend = new Money(200_000) };

        var discount = coupon.Validate(new Money(200_000), Now);

        Assert.Equal(new Money(20_000), discount);
    }
}
