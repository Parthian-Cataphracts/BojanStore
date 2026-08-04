namespace Bojan.Application.Accounts;

/// <summary>
/// What the store will accept as a wallet top-up.
/// </summary>
public sealed class WalletOptions
{
    public const string SectionName = "Wallet";

    /// <summary>
    /// Whether customers may file a card-to-card transfer for an operator to
    /// confirm.
    /// </summary>
    /// <remarks>
    /// Off. The flow is built and tested, but card-to-card means a person
    /// reading a bank statement and deciding whether an image is genuine, and
    /// the store is not running that desk. Turning it on is a business
    /// decision — staff to review the queue — not a deployment one, which is
    /// why it defaults to refusing rather than to whatever the environment
    /// happens to say.
    /// </remarks>
    public bool ManualTopUpEnabled { get; set; }

    /// <summary>Smallest top-up accepted, in Toman.</summary>
    public long MinimumAmount { get; set; } = 10_000;

    /// <summary>
    /// Largest top-up accepted in one request, in Toman.
    /// </summary>
    /// <remarks>
    /// A ceiling on a single request, not on the balance. It bounds what one
    /// mistaken approval can put into a wallet, and what a typo can ask an
    /// operator to approve.
    /// </remarks>
    public long MaximumAmount { get; set; } = 50_000_000;

    /// <summary>
    /// Whether a card-to-card request must carry a receipt image.
    /// </summary>
    /// <remarks>
    /// On, so the operator reviewing the queue always has something to check
    /// the transfer against rather than only the customer's word for it.
    /// </remarks>
    public bool RequireReceipt { get; set; } = true;
}
