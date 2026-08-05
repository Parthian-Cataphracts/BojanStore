using Bojan.Domain.Catalogue;
using Bojan.Domain.Support;

namespace Bojan.Application.Support;

public interface ISupportRepository
{
    void AddTicket(SupportTicket ticket);

    Task<SupportTicket?> FindTicketAsync(Guid ticketId, CancellationToken cancellationToken);

    /// <summary>The thread with its messages loaded, for the panel's detail screen and for replying.</summary>
    Task<SupportTicket?> FindTicketWithMessagesAsync(Guid ticketId, CancellationToken cancellationToken);

    /// <summary>Tracks a message an already-loaded ticket just produced — see <c>IAdminRepository.AddOrderTimelineEvent</c>.</summary>
    void AddMessage(SupportMessage message);

    Task<CannedReply?> FindCannedReplyAsync(Guid id, CancellationToken cancellationToken);

    void AddCannedReply(CannedReply reply);
}

/// <summary>
/// Writes that anyone may make against a product — the stock alert on screen
/// 87.
/// </summary>
/// <remarks>
/// Separate from <c>IAccountRepository</c> because the frontend's allow-list
/// marks this one <c>private: false</c>: there is no customer to hang it off.
/// </remarks>
public interface IStockAlertRepository
{
    Task<Guid?> FindProductIdBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <summary>True when this contact is already waiting on this product — a repeat request must not queue a second SMS.</summary>
    Task<bool> ExistsAsync(Guid productId, string? phone, string? email, CancellationToken cancellationToken);

    void Add(StockAlert alert);

    /// <summary>
    /// Everyone still waiting to hear that this product is back.
    /// </summary>
    /// <remarks>
    /// Only the ones with an address and no <c>NotifiedAtUtc</c>. That column
    /// has existed since the entity was written and nothing ever set it — the
    /// requests were collected and never acted on, so a shopper who asked to be
    /// told was simply never told.
    /// </remarks>
    Task<IReadOnlyList<StockAlert>> ListPendingAsync(Guid productId, CancellationToken cancellationToken);
}
