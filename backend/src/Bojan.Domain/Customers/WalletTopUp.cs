using Bojan.Domain.Common;

namespace Bojan.Domain.Customers;

/// <summary>How the customer says they paid for a top-up.</summary>
public enum WalletTopUpMethod
{
    /// <summary>Redirected to the payment gateway; the gateway's verification decides it.</summary>
    Gateway,

    /// <summary>
    /// Card-to-card, settled out of band. The customer files the transfer with
    /// its tracking number and a receipt, and an operator confirms it against
    /// the bank statement before anything is credited.
    /// </summary>
    Manual,
}

public enum WalletTopUpStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// A request to put money into the wallet, and the record of who decided it.
/// </summary>
/// <remarks>
/// <para>
/// The balance on <see cref="Customer"/> is only ever moved by
/// <see cref="Approve"/>. Filing a request writes this row and nothing else,
/// so there is no path — gateway, card-to-card, or an operator's slip — that
/// credits spendable balance before the money is known to have arrived. That
/// is the whole point of the type: wallet balance buys real goods, so crediting
/// it on a customer's say-so is the same as giving the goods away.
/// </para>
/// <para>
/// A decision is one-way. <see cref="Approve"/> and <see cref="Reject"/> both
/// refuse to act on a request that is not <see cref="WalletTopUpStatus.Pending"/>,
/// which is what makes a retried callback or a double-clicked approve button
/// harmless: the second call changes nothing rather than crediting twice.
/// </para>
/// </remarks>
public sealed class WalletTopUp : Entity
{
    public required Guid CustomerId { get; init; }

    public required Money Amount { get; init; }

    public required WalletTopUpMethod Method { get; init; }

    public WalletTopUpStatus Status { get; private set; } = WalletTopUpStatus.Pending;

    /// <summary>The gateway's reference for this attempt. Null for a card-to-card transfer.</summary>
    public string? GatewayReference { get; init; }

    /// <summary>Where the uploaded receipt image lives. Required for card-to-card when the store asks for one.</summary>
    public string? ReceiptUrl { get; init; }

    /// <summary>The bank's tracking number for the transfer, as the customer entered it.</summary>
    public string? TrackingNumber { get; init; }

    /// <summary>The day the customer says they made the transfer.</summary>
    public DateOnly? PaidOn { get; init; }

    public string? CustomerNote { get; init; }

    /// <summary>The operator who decided it. Null while pending, and for a gateway approval — that one has no operator.</summary>
    public Guid? ReviewedByAdminId { get; private set; }

    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    /// <summary>Why it was rejected, shown to the customer.</summary>
    public string? ReviewNote { get; private set; }

    /// <summary>
    /// The ledger row this request owns.
    /// </summary>
    /// <remarks>
    /// Written as <see cref="WalletTransactionStatus.Pending"/> when the request
    /// is filed, so the customer sees the top-up on their wallet screen while it
    /// waits, and moved to Success or Failed by the decision. One row, not two:
    /// a request and its ledger line describing the same money with separate
    /// amounts is a reconciliation bug waiting to be written.
    /// </remarks>
    public Guid? WalletTransactionId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Accepts the request. The caller credits the wallet — this only records
    /// that it may.
    /// </summary>
    /// <param name="reviewedByAdminId">
    /// The operator who approved it, or null when the gateway's own
    /// verification did.
    /// </param>
    /// <returns>
    /// False when the request was already decided, in which case nothing
    /// changed and the caller must not credit anything.
    /// </returns>
    public bool Approve(Guid? reviewedByAdminId, DateTimeOffset nowUtc, string? note = null)
    {
        if (Status != WalletTopUpStatus.Pending)
        {
            return false;
        }

        Status = WalletTopUpStatus.Approved;
        ReviewedByAdminId = reviewedByAdminId;
        ReviewedAtUtc = nowUtc;
        ReviewNote = note;
        return true;
    }

    /// <returns>False when the request was already decided.</returns>
    public bool Reject(Guid? reviewedByAdminId, DateTimeOffset nowUtc, string? note = null)
    {
        if (Status != WalletTopUpStatus.Pending)
        {
            return false;
        }

        Status = WalletTopUpStatus.Rejected;
        ReviewedByAdminId = reviewedByAdminId;
        ReviewedAtUtc = nowUtc;
        ReviewNote = note;
        return true;
    }
}
