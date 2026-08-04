using Bojan.Domain.Support;

namespace Bojan.Application.Support;

public sealed record LiveChatMessageDto(string Id, bool FromSupport, string Body, DateTimeOffset SentAtUtc);

/// <summary>One row of the panel's live-chat conversation list.</summary>
public sealed record LiveChatConversationDto(
    string VisitorId,
    string LastMessage,
    DateTimeOffset LastMessageAt,
    bool LastFromSupport,
    int UnreadCount);

public interface ILiveChatQueries
{
    Task<IReadOnlyList<LiveChatMessageDto>> ListMessagesAsync(Guid visitorId, CancellationToken cancellationToken);

    /// <summary>Newest-active-first, one row per visitor who has ever written in.</summary>
    Task<IReadOnlyList<LiveChatConversationDto>> ListConversationsAsync(CancellationToken cancellationToken);
}

public interface ILiveChatRepository
{
    void AddMessage(LiveChatMessage message);

    /// <summary>
    /// Marks one side's unread messages read. <paramref name="markSupportMessages"/>
    /// true marks the operator's replies (the visitor just read them); false
    /// marks the visitor's messages (an operator just opened the thread).
    /// </summary>
    Task MarkReadAsync(Guid visitorId, bool markSupportMessages, CancellationToken cancellationToken);
}
