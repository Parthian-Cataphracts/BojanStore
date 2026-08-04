using Bojan.Application.Support;
using Bojan.Domain.Support;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Repositories;

public sealed class LiveChatRepository(BojanDbContext db) : ILiveChatRepository
{
    public void AddMessage(LiveChatMessage message) => db.LiveChatMessages.Add(message);

    public async Task MarkReadAsync(Guid visitorId, bool markSupportMessages, CancellationToken cancellationToken)
    {
        var unread = await db.LiveChatMessages
            .Where(m => m.VisitorId == visitorId && m.FromSupport == markSupportMessages && !m.Read)
            .ToListAsync(cancellationToken);

        foreach (var message in unread)
        {
            message.Read = true;
        }
    }
}
