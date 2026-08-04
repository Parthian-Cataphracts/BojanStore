using Bojan.Domain.Common;

namespace Bojan.Domain.Orders;

/// <summary>
/// How much of an order the wallet pays, and how much is left to collect.
/// </summary>
/// <param name="FromWallet">Taken from the balance. Never more than the balance, never more than the order.</param>
/// <param name="Remainder">What the gateway must still collect. Zero when the wallet covered it.</param>
public readonly record struct WalletSplit(Money FromWallet, Money Remainder)
{
    /// <summary>True when nothing is left to pay — the order is settled on placement.</summary>
    public bool FullyCovered => Remainder == Money.Zero;

    /// <summary>
    /// Splits <paramref name="payable"/> across the wallet and whatever pays
    /// the rest.
    /// </summary>
    /// <remarks>
    /// Three cases, and they are the same expression: the wallet contributes
    /// the lesser of its balance and the bill. Below the balance, the shortfall
    /// is taken and the rest of the balance stays; exactly the balance, and it
    /// empties; above it, the wallet empties and the difference goes to the
    /// gateway. Written once here, rather than as branches at the call site,
    /// because getting it wrong in either direction either gives goods away or
    /// charges twice for them.
    /// </remarks>
    public static WalletSplit For(Money payable, Money walletBalance, bool useWallet)
    {
        if (!useWallet || walletBalance == Money.Zero)
        {
            return new WalletSplit(Money.Zero, payable);
        }

        var fromWallet = walletBalance < payable ? walletBalance : payable;
        return new WalletSplit(fromWallet, payable - fromWallet);
    }
}
