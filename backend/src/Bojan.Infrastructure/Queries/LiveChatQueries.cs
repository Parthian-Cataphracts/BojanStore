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

    public async Task<IReadOnlyList<LiveChatConversationDto>> ListConversationsAsync(CancellationToken cancellationToken)
    {
        var conversations = await db.LiveChatMessages
            .AsNoTracking()
            .GroupBy(m => m.VisitorId)
            .Select(g => new
            {
                VisitorId = g.Key,
                Last = g.OrderByDescending(m => m.SentAtUtc).First(),
                UnreadCount = g.Count(m => !m.FromSupport && !m.Read),
            })
            .OrderByDescending(g => g.Last.SentAtUtc)
            .ToListAsync(cancellationToken);

        return conversations
            .Select(g => new LiveChatConversationDto(
                g.VisitorId.ToString(), g.Last.Body, g.Last.SentAtUtc, g.Last.FromSupport, g.UnreadCount))
            .ToList();
    }
}
