using Bojan.Application.Contracts;

namespace Bojan.Application.Notifications;

/// <summary>
/// The shop's Web Push identity, in the settings table beside SMS and the
/// mailbox.
/// </summary>
public interface IWebPushSettingsStore
{
    /// <summary>What the panel may see — never the private key.</summary>
    Task<WebPushSettingsDto> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(bool enabled, string subject, CancellationToken cancellationToken);

    /// <summary>
    /// Mints a fresh key pair and stores it.
    /// </summary>
    /// <remarks>
    /// Replacing the pair orphans every existing subscription: browsers recorded
    /// the old public key when they agreed, and a message signed by the new one
    /// is from a stranger as far as they are concerned. So this is an explicit
    /// action on the settings screen with that consequence written on it, never
    /// something that happens as a side effect of saving.
    /// </remarks>
    Task<WebPushSettingsDto> GenerateKeysAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Sends one sealed notification to one browser.
/// </summary>
/// <remarks>
/// Swallows its own failures, like <see cref="Auth.ISmsSender"/> and the
/// mailer: a browser that has been closed for a month, or a push service having
/// an outage, must not take down the dispatch of everybody else's. A
/// subscription the push service reports as gone is deleted rather than retried
/// forever — that is the one failure worth acting on.
/// </remarks>
public interface IWebPushSender
{
    /// <returns>True when the push service accepted the message.</returns>
    Task<bool> SendAsync(Guid subscriptionId, PushMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Sends one notification to every browser a customer has registered.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="ICustomerMailer"/>, and it exists for the same
/// reason: the transactional paths — an order that shipped, a payment that
/// landed — should say "tell this customer" without knowing how many devices
/// that is or what happens when one of them is unreachable. Never throws, and
/// never delays the caller's own work; a customer with no browser listening, or
/// a shop with push switched off, is a silent no-op.
/// </remarks>
public interface ICustomerPushNotifier
{
    Task NotifyAsync(Guid customerId, PushMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// What a notification says once it reaches a browser.
/// </summary>
/// <param name="Link">
/// Where clicking it lands, relative to the storefront. Relative rather than
/// absolute so the same message works on whatever origin the shop is served
/// from, and so nothing that composes one can send a customer off-site.
/// </param>
public sealed record PushMessage(string Title, string Body, string? Link = null);
