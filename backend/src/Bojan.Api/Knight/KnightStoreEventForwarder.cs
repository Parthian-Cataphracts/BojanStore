using System.Text.Json;
using Bojan.Application.Common;
using Bojan.Infrastructure.Persistence;
using Bojan.Infrastructure.Persistence.Outbox;

namespace Bojan.Api.Knight;

/// <summary>
/// The integration-side <see cref="IStoreEventForwarder"/>: it does not deliver,
/// it <b>persists</b>. A business event becomes a row in the outbox, and the
/// <see cref="OutboxDispatcher"/> is what carries it to the feature services and
/// retries until they accept it.
///
/// Persisting rather than POSTing here is what makes forwarding survive a restart
/// or a feature service being down: the old fire-and-forget lost the event if the
/// process stopped before a handful of in-memory retries succeeded. The write is
/// its own short transaction, right after the domain change committed; the only
/// unguarded moment left is a crash in the sub-millisecond between those two
/// commits, which the transactional-outbox refinement in the TODO closes.
/// </summary>
public sealed class KnightStoreEventForwarder(
    IServiceScopeFactory scopes,
    ILogger<KnightStoreEventForwarder> logger) : IStoreEventForwarder
{
    public async Task ForwardAsync(string eventName, object payload, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();

            var now = DateTimeOffset.UtcNow;
            db.OutboxEvents.Add(new OutboxEvent
            {
                EventName = eventName,
                PayloadJson = JsonSerializer.Serialize(payload),
                CreatedAtUtc = now,
                Status = OutboxStatus.Pending,
                NextAttemptUtc = now,
            });

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Enqueueing must never fail the action that raised the event. A lost
            // enqueue is the one gap the outbox cannot cover, so it is logged loudly.
            logger.LogError(exception, "Could not enqueue {Event} to the outbox.", eventName);
        }
    }
}
