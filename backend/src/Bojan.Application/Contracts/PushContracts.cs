namespace Bojan.Application.Contracts;

/// <summary>
/// The Web Push settings as the panel shows them.
/// </summary>
/// <param name="PublicKey">
/// Deliberately readable. It is the key browsers subscribe against, the
/// storefront has to hand it to every visitor, and it is public by design —
/// unlike the merchant id or the SMS key, hiding it would only hide it from the
/// operator.
/// </param>
/// <param name="HasPrivateKey">
/// Whether the other half exists. The key itself is never returned, in either
/// direction: anyone holding it can send notifications in the shop's name to
/// every browser that ever subscribed.
/// </param>
/// <param name="Subject">
/// A <c>mailto:</c> or <c>https:</c> the push services can use to reach the shop
/// about its traffic. Required by RFC 8292, and some services refuse messages
/// without it.
/// </param>
public sealed record WebPushSettingsDto(bool Enabled, string PublicKey, bool HasPrivateKey, string Subject);

/// <summary>What the settings screen posts.</summary>
public sealed record SaveWebPushSettingsRequest(bool Enabled, string Subject);

/// <summary>
/// A browser handing over what it takes to reach it.
/// </summary>
/// <remarks>
/// The three fields come straight from the browser's own
/// <c>PushSubscription</c> — the endpoint it minted and the two keys that seal
/// messages to it. Nothing here is chosen by the page.
/// </remarks>
public sealed record SavePushSubscriptionRequest(string Endpoint, string P256dh, string Auth);

/// <summary>What the storefront needs before it can ask the browser to subscribe.</summary>
/// <remarks>
/// <paramref name="Enabled"/> is false when the owner has not switched push on
/// or has not generated keys. The storefront shows nothing at all in that case
/// rather than a control that cannot work.
/// </remarks>
public sealed record PushAvailabilityDto(bool Enabled, string PublicKey);
