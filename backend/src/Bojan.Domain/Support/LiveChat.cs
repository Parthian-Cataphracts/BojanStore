using Bojan.Domain.Common;

namespace Bojan.Domain.Support;

/// <summary>
/// One message in a visitor's live-chat conversation with support — the
/// widget on the storefront and the panel screen that answers it.
/// </summary>
/// <remarks>
/// A visitor is identified by <see cref="VisitorId"/>, an opaque id the
/// widget mints on first open and keeps client-side — not a session, so an
/// anonymous shopper can chat without an account, the same way the contact
/// form on <see cref="SupportTicket"/> allows an anonymous sender.
/// <see cref="CustomerId"/> is attached when the visitor happens to be signed
/// in, purely so a future screen could surface the conversation on their
/// account; nothing reads it yet.
/// </remarks>
public sealed class LiveChatMessage : Entity
{
    public required Guid VisitorId { get; init; }

    public Guid? CustomerId { get; init; }

    public required string Body { get; init; }

    /// <summary>True when an operator wrote it.</summary>
    public required bool FromSupport { get; init; }

    public required DateTimeOffset SentAtUtc { get; init; }

    /// <summary>
    /// Read by the side that didn't write it — true on a visitor message once
    /// an operator has opened the conversation, true on a support message
    /// once the widget has fetched it.
    /// </summary>
    public bool Read { get; set; }
}
