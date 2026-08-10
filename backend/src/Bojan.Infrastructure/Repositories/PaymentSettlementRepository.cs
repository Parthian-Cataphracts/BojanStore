using Bojan.Application.Payments;
using Bojan.Domain.Orders;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Repositories;

/// <summary>
/// The orders a gateway callback can be about.
/// </summary>
/// <remarks>
/// Its own repository rather than more methods on the checkout's: settling a
/// payment happens minutes or hours after the basket is gone, on a request the
/// checkout knows nothing about, and one of its two callers is a background
/// worker with no shopper attached at all.
/// </remarks>
public sealed class PaymentSettlementRepository(BojanDbContext db) : IPaymentSettlementRepository
{
    /// <inheritdoc cref="IPaymentSettlementRepository.SetPaymentSessionAsync"/>
    public Task SetPaymentSessionAsync(
        Guid orderId,
        string paymentUrl,
        string reference,
        CancellationToken cancellationToken) =>
        // Two columns on one row, by key. Nothing else about the order can have
        // changed between the commit and this call, so there is nothing to
        // reconcile, and loading the aggregate to set two strings would take a
        // second read for no reason.
        db.Orders
            .Where(order => order.Id == orderId)
            .ExecuteUpdateAsync(
                order => order
                    .SetProperty(o => o.PaymentUrl, paymentUrl)
                    .SetProperty(o => o.PaymentReference, reference),
                cancellationToken);

    /// <inheritdoc cref="IPaymentSettlementRepository.AddNotification"/>
    public void AddNotification(Domain.Customers.CustomerNotification notification) =>
        db.CustomerNotifications.Add(notification);

    /// <inheritdoc cref="IPaymentSettlementRepository.PeekByReferenceAsync"/>
    /// <remarks>
    /// Untracked, for the reason the wallet's equivalent peek is: the instance
    /// decided has to be the one returned under the row lock, and a tracked
    /// read here would have EF hand that second query this same stale instance
    /// back out of the change tracker.
    /// </remarks>
    public Task<Order?> PeekByReferenceAsync(
        Guid customerId,
        string reference,
        CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking().FirstOrDefaultAsync(
            order => order.CustomerId == customerId && order.PaymentReference == reference,
            cancellationToken);

    /// <inheritdoc cref="IPaymentSettlementRepository.FindByReferenceForUpdateAsync"/>
    public async Task<Order?> FindByReferenceForUpdateAsync(string reference, CancellationToken cancellationToken)
    {
        // The lock that makes Order.MarkPaid's status check mean something. The
        // subquery is here because the lock is taken by reference while the row
        // is identified by key — see AccountRepository for why SQLite is left
        // out of it.
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM orders WHERE "PaymentReference" = {reference} FOR UPDATE""",
                cancellationToken);
        }

        return await db.Orders.FirstOrDefaultAsync(
            order => order.PaymentReference == reference,
            cancellationToken);
    }

    /// <inheritdoc cref="IPaymentSettlementRepository.ListUnsettledAsync"/>
    public async Task<IReadOnlyList<Order>> ListUnsettledAsync(
        DateTimeOffset placedBeforeUtc,
        DateTimeOffset placedAfterUtc,
        int limit,
        CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking()
            .Where(order =>
                order.PaymentStatus == OrderPaymentStatus.AwaitingPayment
                && order.Status != OrderStatus.Cancelled
                && order.PaymentReference != null
                && order.PlacedAtUtc < placedBeforeUtc
                && order.PlacedAtUtc > placedAfterUtc)
            .OrderBy(order => order.PlacedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
