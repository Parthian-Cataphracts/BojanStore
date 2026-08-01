using Bojan.Domain.Common;

namespace Bojan.Domain.Marketing;

/// <summary>The frontend's <c>Campaign.kind</c> — <c>'discount' | 'banner' | 'email' | 'sms'</c>.</summary>
public enum CampaignKind
{
    Discount,
    Banner,
    Email,
    Sms,
}

/// <summary>The frontend's <c>Campaign.status</c> — <c>'scheduled' | 'running' | 'ended'</c>.</summary>
public enum CampaignStatus
{
    Scheduled,
    Running,
    Ended,
}

/// <summary>A marketing campaign — screens 128-132.</summary>
public sealed class Campaign : SoftDeletableEntity
{
    public required string Title { get; set; }

    public required CampaignKind Kind { get; set; }

    public CampaignStatus Status { get; set; } = CampaignStatus.Scheduled;

    public string? Description { get; set; }

    public DateTimeOffset? StartsAtUtc { get; set; }

    public DateTimeOffset? EndsAtUtc { get; set; }

    /// <summary>People the campaign reached — recorded by whatever dispatches it, not computed here.</summary>
    public int Reach { get; set; }

    /// <summary>Orders attributed to the campaign.</summary>
    public int Conversion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Channels the panel's notification composer can send on — screen 143.</summary>
public enum NotificationChannel
{
    InApp,
    Email,
    Sms,
    Push,
}

/// <summary>
/// A broadcast an operator composed — the <c>notifications</c> admin write.
/// </summary>
/// <remarks>
/// Queued rather than sent inline: <see cref="ScheduledAtUtc"/> may be in the
/// future, and even "send now" fans out to every customer in the audience,
/// which is not work to do inside a request. Dispatch turns each one into the
/// per-customer <c>CustomerNotification</c> rows screen 53 reads.
/// </remarks>
public sealed class NotificationCampaign : Entity
{
    public required NotificationChannel Channel { get; init; }

    /// <summary>Who receives it — "all", a customer group name, or a segment key.</summary>
    public required string Audience { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public DateTimeOffset? ScheduledAtUtc { get; init; }

    public DateTimeOffset? SentAtUtc { get; set; }

    public required Guid ActorId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
