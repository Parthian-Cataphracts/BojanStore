using Bojan.Application.Support;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Queries;

public sealed class LiveChatQueries(BojanDbContext db) : ILiveChatQueries
{
    public async Task<IReadOnlyList<LiveChatMessageDto>> ListMessagesAsync(
        Guid visitorId, CancellationToken cancellationToken) =>
        await db.LiveChatMessages
            .AsNoTracking()
            .Where(m => m.VisitorId == visitorId)
            .OrderBy(m => m.SentAtUtc)
            .Select(m => new LiveChatMessageDto(m.Id.ToString(), m.FromSupport, m.Body, m.SentAtUtc, m.Read))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// One row per visitor who has ever written in, newest activity first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written as "the message no later message follows" rather than as a
    /// group whose last element is picked out. The grouped form read better
    /// and did not run: <c>GroupBy(...).Select(g =&gt; g.OrderByDescending(...).First())</c>
    /// pulls a whole <em>entity</em> out of a group, which EF cannot put into
    /// SQL, so the panel's conversation queue answered 500 to every request —
    /// the operator's live-chat screen was an error page and every message a
    /// shopper sent went unanswered.
    /// </para>
    /// <para>
    /// The anti-join is a total order, not just a comparison on time: two
    /// messages sharing a timestamp to the tick would each have no later
    /// message and the conversation would appear in the queue twice. Ties
    /// break on the id, so exactly one row survives per visitor whatever the
    /// clock does.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<LiveChatConversationDto>> ListConversationsAsync(CancellationToken cancellationToken) =>
        await db.LiveChatMessages
            .AsNoTracking()
            .Where(m => !db.LiveChatMessages.Any(later =>
                later.VisitorId == m.VisitorId
                && (later.SentAtUtc > m.SentAtUtc
                    || (later.SentAtUtc == m.SentAtUtc && later.Id > m.Id))))
            .OrderByDescending(m => m.SentAtUtc)
            .Select(m => new LiveChatConversationDto(
                m.VisitorId.ToString(),
                m.Body,
                m.SentAtUtc,
                m.FromSupport,
                // What the operator still has to answer: the visitor's own
                // messages that no operator has opened the thread on yet.
                db.LiveChatMessages.Count(unread =>
                    unread.VisitorId == m.VisitorId && !unread.FromSupport && !unread.Read)))
            .ToListAsync(cancellationToken);
}
