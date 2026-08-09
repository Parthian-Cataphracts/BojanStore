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
    /// <summary>
    /// The widget reading its conversation. Marks nothing.
    /// </summary>
    /// <remarks>
    /// Fetching used to mark every operator reply read, which made the two
    /// indistinguishable — and the widget polls this while it is <em>closed</em>
    /// so it can badge the launcher, so the badge marked its own subject read
    /// and could never show anything. Reading is now something the visitor
    /// does, not something the poll does: see <see cref="MarkReadAsVisitorAsync"/>,
    /// which the widget calls when the panel is actually open in front of them.
    /// </remarks>
    public Task<IReadOnlyList<LiveChatMessageDto>> GetConversationAsVisitorAsync(
        Guid visitorId, CancellationToken cancellationToken) =>
        queries.ListMessagesAsync(visitorId, cancellationToken);

    /// <summary>The visitor has the panel open — the operator's replies are read.</summary>
    public async Task MarkReadAsVisitorAsync(Guid visitorId, CancellationToken cancellationToken)
    {
        await chat.MarkReadAsync(visitorId, markSupportMessages: true, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
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
