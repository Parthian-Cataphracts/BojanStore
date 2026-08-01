using Bojan.Domain.Common;

namespace Bojan.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Constructor_rejects_negative_amounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-1));
    }

    [Fact]
    public void Subtraction_throws_when_it_would_go_negative()
    {
        var small = new Money(100);
        var large = new Money(200);

        Assert.Throws<InvalidOperationException>(() => small - large);
    }

    [Fact]
    public void ClampedMinus_never_goes_below_zero()
    {
        var small = new Money(100);
        var large = new Money(200);

        Assert.Equal(Money.Zero, small.ClampedMinus(large));
    }

    [Fact]
    public void Multiplication_by_quantity_scales_correctly()
    {
        var unitPrice = new Money(50_000);

        Assert.Equal(new Money(150_000), unitPrice * 3);
    }

    [Fact]
    public void Multiplication_by_negative_quantity_throws()
    {
        var unitPrice = new Money(50_000);

        Assert.Throws<ArgumentOutOfRangeException>(() => unitPrice * -1);
    }

    [Fact]
    public void Comparisons_order_by_amount()
    {
        var small = new Money(1);
        var large = new Money(2);

        var alsoSmall = new Money(1);

        Assert.True(small < large);
        Assert.True(large > small);
        Assert.True(small <= alsoSmall);
        Assert.True(small >= alsoSmall);
    }
}
