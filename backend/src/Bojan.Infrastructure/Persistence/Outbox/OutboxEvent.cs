namespace Bojan.Infrastructure.Persistence.Outbox;

/// <summary>
/// A store event queued for delivery to KNIGHT's feature services.
///
/// The durable half of forwarding: the event is written here so it survives a
/// restart, and a background dispatcher retries it until every subscriber has
/// accepted it. Without this, a crash between announcing an order and the feature
/// service receiving it would lose a delivery — a loyalty point never awarded, an
/// analytics event never counted — with nothing to replay it from.
/// </summary>
public sealed class OutboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The canonical event name, e.g. <c>order.paid</c>.</summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>The event payload, exactly as it will be delivered.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary><c>Pending</c>, <c>Delivered</c> or <c>Failed</c>.</summary>
    public string Status { get; set; } = OutboxStatus.Pending;

    public int Attempts { get; set; }

    /// <summary>The earliest a dispatcher should try this row again.</summary>
    public DateTimeOffset NextAttemptUtc { get; set; }

    public DateTimeOffset? DeliveredAtUtc { get; set; }

    /// <summary>Why the last attempt did not fully deliver, for the operator.</summary>
    public string? LastError { get; set; }
}

public static class OutboxStatus
{
    public const string Pending = "Pending";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
}
