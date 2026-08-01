using Bojan.Domain.Common;

namespace Bojan.Domain.Support;

/// <summary>The frontend's <c>TicketStatus</c> — <c>'open' | 'answered' | 'closed'</c>, screens 54 and 148.</summary>
public enum SupportTicketStatus
{
    Open,
    Answered,
    Closed,
}

/// <summary>Triage priority, shown in the panel's thread list (<c>SupportThread.priority</c>).</summary>
public enum SupportPriority
{
    Low,
    Normal,
    High,
}

/// <summary>
/// A support conversation.
/// </summary>
/// <remarks>
/// One entity serves both sides: the customer reads it as
/// <c>SupportTicket</c> (screen 54) and an operator reads the same row as
/// <c>SupportThread</c> (screen 148, <c>apps/admin/src/lib/types.ts</c>). The
/// two DTOs differ only in which fields they project.
///
/// <see cref="CustomerId"/> is nullable because the public contact form
/// (<c>POST /support/messages</c>) is allow-listed as <c>private: false</c> —
/// a visitor with no account may still write in, and their name, phone and
/// email arrive in the body instead.
/// </remarks>
public sealed class SupportTicket : Entity
{
    public Guid? CustomerId { get; init; }

    /// <summary>Who wrote in, captured at submission — the contact form supplies this for anonymous senders.</summary>
    public required string ContactName { get; set; }

    public string? ContactPhone { get; set; }

    public string? ContactEmail { get; set; }

    public required string Subject { get; set; }

    public SupportTicketStatus Status { get; private set; } = SupportTicketStatus.Open;

    public SupportPriority Priority { get; set; } = SupportPriority.Normal;

    public Guid? AssigneeId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private readonly List<SupportMessage> _messages = [];
    public IReadOnlyCollection<SupportMessage> Messages => _messages;

    /// <summary>
    /// Appends a message and moves the status with it: a reply from support
    /// answers the ticket, a reply from the customer re-opens it. A closed
    /// ticket that the customer writes back to becomes open again rather than
    /// silently swallowing the message.
    /// </summary>
    public SupportMessage AddMessage(string body, bool fromSupport, DateTimeOffset nowUtc)
    {
        var message = new SupportMessage
        {
            TicketId = Id,
            Body = body,
            FromSupport = fromSupport,
            SentAtUtc = nowUtc,
        };

        _messages.Add(message);
        UpdatedAtUtc = nowUtc;
        Status = fromSupport ? SupportTicketStatus.Answered : SupportTicketStatus.Open;
        return message;
    }

    public void Close(DateTimeOffset nowUtc)
    {
        Status = SupportTicketStatus.Closed;
        UpdatedAtUtc = nowUtc;
    }
}

public sealed class SupportMessage : Entity
{
    public required Guid TicketId { get; init; }

    public required string Body { get; init; }

    /// <summary>True when an operator wrote it — the customer's card shows "lastMessageFromSupport".</summary>
    public required bool FromSupport { get; init; }

    public required DateTimeOffset SentAtUtc { get; init; }
}

/// <summary>
/// A saved reply an operator can drop into a thread — screen 149, backing the
/// <c>support/canned-replies</c> admin resource.
/// </summary>
public sealed class CannedReply : SoftDeletableEntity
{
    public required string Title { get; set; }

    public required string Body { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
