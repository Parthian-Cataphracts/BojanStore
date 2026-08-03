using Bojan.Domain.Common;
using Bojan.Domain.Orders;

namespace Bojan.Domain.Tests;

public class WalletSplitTests
{
    private static Money T(long amount) => new(amount);

    [Fact]
    public void Balance_above_the_bill_pays_all_of_it_and_keeps_the_rest()
    {
        var split = WalletSplit.For(payable: T(300_000), walletBalance: T(500_000), useWallet: true);

        Assert.Equal(T(300_000), split.FromWallet);
        Assert.Equal(Money.Zero, split.Remainder);
        Assert.True(split.FullyCovered);
    }

    [Fact]
    public void Balance_equal_to_the_bill_empties_the_wallet_exactly()
    {
        var split = WalletSplit.For(payable: T(300_000), walletBalance: T(300_000), useWallet: true);

        Assert.Equal(T(300_000), split.FromWallet);
        Assert.Equal(Money.Zero, split.Remainder);
        Assert.True(split.FullyCovered);
    }

    [Fact]
    public void Balance_below_the_bill_is_spent_in_full_and_the_difference_is_left_to_collect()
    {
        var split = WalletSplit.For(payable: T(300_000), walletBalance: T(120_000), useWallet: true);

        Assert.Equal(T(120_000), split.FromWallet);
        Assert.Equal(T(180_000), split.Remainder);
        Assert.False(split.FullyCovered);
    }

    [Fact]
    public void Declining_the_wallet_leaves_the_whole_bill_to_the_gateway()
    {
        var split = WalletSplit.For(payable: T(300_000), walletBalance: T(500_000), useWallet: false);

        Assert.Equal(Money.Zero, split.FromWallet);
        Assert.Equal(T(300_000), split.Remainder);
    }

    [Fact]
    public void An_empty_wallet_contributes_nothing()
    {
        var split = WalletSplit.For(payable: T(300_000), walletBalance: Money.Zero, useWallet: true);

        Assert.Equal(Money.Zero, split.FromWallet);
        Assert.Equal(T(300_000), split.Remainder);
    }

    [Fact]
    public void A_free_order_takes_nothing_from_a_funded_wallet()
    {
        var split = WalletSplit.For(payable: Money.Zero, walletBalance: T(500_000), useWallet: true);

        Assert.Equal(Money.Zero, split.FromWallet);
        Assert.Equal(Money.Zero, split.Remainder);
        Assert.True(split.FullyCovered);
    }

    /// <summary>
    /// The invariant the two halves have to keep, whatever the inputs: the
    /// customer is charged the bill exactly once, across both methods.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(1, 999_999)]
    [InlineData(999_999, 1)]
    [InlineData(250_000, 250_000)]
    [InlineData(250_001, 250_000)]
    [InlineData(250_000, 250_001)]
    public void The_two_halves_always_add_up_to_the_bill(long payable, long balance)
    {
        var split = WalletSplit.For(T(payable), T(balance), useWallet: true);

        Assert.Equal(payable, split.FromWallet.Amount + split.Remainder.Amount);
        // And never spends money the customer does not have.
        Assert.True(split.FromWallet.Amount <= balance);
    }
}
