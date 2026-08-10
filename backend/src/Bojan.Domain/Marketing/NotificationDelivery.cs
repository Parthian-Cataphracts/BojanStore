using Bojan.Domain.Common;

namespace Bojan.Domain.Marketing;

/// <summary>
/// One recipient of one campaign, on a channel that leaves nothing else behind.
/// </summary>
/// <remarks>
/// <para>
/// The in-app channel is resumable for free: its fan-out writes a
/// <c>CustomerNotification</c> per recipient, so an attempt that fails halfway
/// can look at what is already there and carry on from it. SMS and email leave
/// no such row — the message is gone the moment the provider accepts it — so
/// without this table a campaign that failed on the ten-thousandth recipient
/// would start again at the first, and everyone already reached would get the
/// same offer a second time.
/// </para>
/// <para>
/// That was tolerable while the only SMS implementation wrote to a log: it cost
/// a duplicate line. With a provider behind it, it costs the shop money per
/// duplicate and costs the customer a message they did not ask for twice — and
/// an Iranian shop sending the same advertising SMS twice is a shop being
/// reported.
/// </para>
/// <para>
/// The unique index on (campaign, customer) is what makes the skip trustworthy
/// rather than merely likely: two dispatch cycles overlapping would otherwise
/// both read an empty set and both send. The channel is recorded alongside for
/// the same reason the campaign carries it — so a delivery can be explained
/// later without joining back to a row that may have been edited.
/// </para>
/// </remarks>
public sealed class NotificationDelivery : Entity
{
    public required Guid CampaignId { get; init; }

    public required Guid CustomerId { get; init; }

    public required NotificationChannel Channel { get; init; }

    public required DateTimeOffset SentAtUtc { get; init; }
}
