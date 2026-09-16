using Bojan.Application.Common;
using Knight.StoreAgent;

namespace Bojan.Api.Knight;

/// <summary>
/// The integration-side implementation of <see cref="IStoreEventForwarder"/>:
/// hands a business event to the vendored agent's forwarder, which delivers it to
/// every subscribed Feature service.
///
/// Fire-and-forget on purpose. The agent's forwarder already retries, but even a
/// bounded retry must not sit on the request that settled an order — so it runs
/// on a background task and its failures are logged, never surfaced to the shop.
/// The one thing this loses is a delivery in flight when the process stops; that
/// is the durable-outbox hardening noted in the delivery TODO, not a reason to
/// make selling wait on a Feature.
/// </summary>
public sealed class KnightStoreEventForwarder(
    IKnightEventForwarder forwarder,
    ILogger<KnightStoreEventForwarder> logger) : IStoreEventForwarder
{
    public Task ForwardAsync(string eventName, object payload, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await forwarder.ForwardAsync(eventName, payload, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Forwarding {Event} to KNIGHT features failed.", eventName);
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }
}
