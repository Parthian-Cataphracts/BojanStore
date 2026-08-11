using Bojan.Domain.Common;

namespace Bojan.Domain.Customers;

/// <summary>
/// One browser that has agreed to receive push notifications.
/// </summary>
/// <remarks>
/// <para>
/// A subscription belongs to a browser, not to a person: the same customer on a
/// phone and a laptop is two rows, and clearing site data on either produces a
/// third rather than updating the first. <see cref="Endpoint"/> is the identity —
/// it is a URL at the browser vendor's own push service, and it is unique across
/// the whole table because two customers cannot share one.
/// </para>
/// <para>
/// <see cref="P256dh"/> and <see cref="Auth"/> are the browser's half of the
/// encryption. Every message is sealed so that only this browser can open it —
/// the push service that carries it never sees the contents. They are stored as
/// the browser hands them over, base64url, because they are public key material
/// and a secret shared with the shop rather than credentials of the shop's own.
/// </para>
/// </remarks>
public sealed class PushSubscription : Entity
{
    public required Guid CustomerId { get; init; }

    /// <summary>The push service URL this browser is reachable at.</summary>
    public required string Endpoint { get; init; }

    /// <summary>The browser's public key, base64url, uncompressed P-256 point.</summary>
    public required string P256dh { get; set; }

    /// <summary>The browser's authentication secret, base64url, 16 bytes.</summary>
    public required string Auth { get; set; }

    /// <summary>
    /// What the browser called itself when it subscribed.
    /// </summary>
    /// <remarks>
    /// Only so a customer looking at their own devices can tell which one is
    /// which. Truncated on the way in and never matched against.
    /// </remarks>
    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When a message was last accepted for this browser.
    /// </summary>
    /// <remarks>
    /// Accepted by the push service, which is as far as the shop can see. Whether
    /// the browser was awake to show it is not something push reports.
    /// </remarks>
    public DateTimeOffset? LastSentAtUtc { get; set; }
}
