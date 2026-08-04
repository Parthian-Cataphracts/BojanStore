using Bojan.Application.Common;
using Bojan.Domain.Support;

namespace Bojan.Application.Support;

/// <summary>
/// The live-chat widget's backend: a visitor's own conversation (no
/// credential, keyed by the opaque id the widget mints) and the panel's side
/// of the same conversation.
/// </summary>
public sealed class LiveChatService(
    ILiveChatRepository chat,
    ILiveChatQueries queries,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    /// <summary>The widget opening its conversation — marks the operator's replies read.</summary>
    public async Task<IReadOnlyList<LiveChatMessageDto>> GetConversationAsVisitorAsync(
        Guid visitorId, CancellationToken cancellationToken)
    {
        await chat.MarkReadAsync(visitorId, markSupportMessages: true, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await queries.ListMessagesAsync(visitorId, cancellationToken);
    }

    /// <summary>An operator opening a thread — marks the visitor's messages read.</summary>
    public async Task<IReadOnlyList<LiveChatMessageDto>> GetConversationAsSupportAsync(
        Guid visitorId, CancellationToken cancellationToken)
    {
        await chat.MarkReadAsync(visitorId, markSupportMessages: false, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await queries.ListMessagesAsync(visitorId, cancellationToken);
    }

    public async Task<IReadOnlyList<LiveChatConversationDto>> ListConversationsAsync(CancellationToken cancellationToken) =>
        await queries.ListConversationsAsync(cancellationToken);

    public async Task SendVisitorMessageAsync(
        Guid visitorId, Guid? customerId, string body, CancellationToken cancellationToken)
    {
        chat.AddMessage(new LiveChatMessage
        {
            VisitorId = visitorId,
            CustomerId = customerId,
            Body = body,
            FromSupport = false,
            SentAtUtc = clock.UtcNow,
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SendSupportReplyAsync(Guid visitorId, string body, CancellationToken cancellationToken)
    {
        chat.AddMessage(new LiveChatMessage
        {
            VisitorId = visitorId,
            Body = body,
            FromSupport = true,
            SentAtUtc = clock.UtcNow,
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
