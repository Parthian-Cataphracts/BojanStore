using Bojan.Application.Notifications;
using Bojan.Domain.Customers;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Repositories;

public sealed class PushSubscriptionRepository(BojanDbContext db) : IPushSubscriptionRepository
{
    public Task<PushSubscription?> FindByEndpointAsync(string endpoint, CancellationToken cancellationToken) =>
        db.PushSubscriptions.FirstOrDefaultAsync(row => row.Endpoint == endpoint, cancellationToken);

    public async Task<IReadOnlyList<PushSubscription>> ListForCustomerAsync(
        Guid customerId, CancellationToken cancellationToken) =>
        await db.PushSubscriptions
            .Where(row => row.CustomerId == customerId)
            .ToListAsync(cancellationToken);

    public void Add(PushSubscription subscription) => db.PushSubscriptions.Add(subscription);

    public void Remove(PushSubscription subscription) => db.PushSubscriptions.Remove(subscription);
}
