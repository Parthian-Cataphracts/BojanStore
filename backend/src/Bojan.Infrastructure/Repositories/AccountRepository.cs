using Bojan.Application.Accounts;
using Bojan.Domain.Customers;
using Bojan.Domain.Orders;
using Bojan.Domain.Reviews;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Repositories;

/// <summary>
/// Phase 5's private writes.
/// </summary>
/// <remarks>
/// Nothing here is <c>AsNoTracking</c>: these entities are loaded to be
/// changed. Every lookup takes the customer id, so an id belonging to someone
/// else resolves to null rather than to a row the caller then has to be
/// stopped from editing.
/// </remarks>
public sealed class AccountRepository(BojanDbContext db) : IAccountRepository
{
    public Task<Customer?> FindAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public Task<Customer?> FindWithAddressesAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.Customers.Include(c => c.Addresses).FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public Task<Address?> FindAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken) =>
        db.Addresses.FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, cancellationToken);

    public void AddAddress(Address address) => db.Addresses.Add(address);

    public void RemoveAddress(Address address) => db.Addresses.Remove(address);

    public async Task<IReadOnlyList<CustomerNotification>> FindNotificationsAsync(
        Guid customerId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var query = db.CustomerNotifications.Where(n => n.CustomerId == customerId && !n.IsRead);

        // An empty id list means "mark everything read" — screen 53's header
        // action, which posts no ids at all.
        if (ids.Count > 0)
        {
            var wanted = ids.ToList();
            query = query.Where(n => wanted.Contains(n.Id));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task<WishlistItem?> FindWishlistItemAsync(Guid customerId, Guid productId, CancellationToken cancellationToken) =>
        db.WishlistItems.FirstOrDefaultAsync(
            i => i.CustomerId == customerId && i.ProductId == productId, cancellationToken);

    public void AddWishlistItem(WishlistItem item) => db.WishlistItems.Add(item);

    public void RemoveWishlistItem(WishlistItem item) => db.WishlistItems.Remove(item);

    public async Task<int> ClearSearchHistoryAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var entries = await db.SearchHistoryEntries
            .Where(e => e.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        db.SearchHistoryEntries.RemoveRange(entries);
        return entries.Count;
    }

    public void AddWalletTransaction(WalletTransaction transaction) => db.WalletTransactions.Add(transaction);

    public void AddWalletTopUp(WalletTopUp topUp) => db.WalletTopUps.Add(topUp);

    public async Task<IReadOnlyList<WalletTopUp>> ListPendingTopUpsAsync(
        Guid customerId,
        CancellationToken cancellationToken) =>
        await db.WalletTopUps.AsNoTracking()
            .Where(t => t.CustomerId == customerId && t.Status == WalletTopUpStatus.Pending)
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    /// <inheritdoc cref="IAccountRepository.FindTopUpByReferenceAsync"/>
    /// <remarks>
    /// Untracked on purpose. This is the unlocked peek that decides whether the
    /// gateway is worth asking at all; the instance that gets approved is the
    /// one <see cref="FindTopUpForUpdateAsync"/> returns under the row lock. If
    /// this read tracked the row, that second query would hand back this same
    /// stale instance out of the change tracker — EF does not overwrite a
    /// tracked entity's values on re-query — and the status check would be
    /// reading pre-lock state again, which is the whole thing the lock exists to
    /// prevent.
    /// </remarks>
    public Task<WalletTopUp?> FindTopUpByReferenceAsync(
        Guid customerId,
        string gatewayReference,
        CancellationToken cancellationToken) =>
        db.WalletTopUps.AsNoTracking().FirstOrDefaultAsync(
            t => t.CustomerId == customerId && t.GatewayReference == gatewayReference,
            cancellationToken);

    /// <inheritdoc cref="IAccountRepository.FindTopUpForUpdateAsync"/>
    public async Task<WalletTopUp?> FindTopUpForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        // The lock that makes WalletTopUp.Approve's status check mean something.
        // Locking the customer row is not a substitute: it serialises the two
        // callers but tells neither that the other already credited this
        // top-up, so both would read Pending and both would credit. The row
        // being decided is the row that has to be locked.
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM wallet_top_ups WHERE "Id" = {id} FOR UPDATE""",
                cancellationToken);
        }

        return await db.WalletTopUps.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<WalletTransaction?> FindWalletTransactionAsync(Guid id, CancellationToken cancellationToken) =>
        db.WalletTransactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <inheritdoc cref="IAccountRepository.FindForUpdateAsync"/>
    public async Task<Customer?> FindForUpdateAsync(Guid customerId, CancellationToken cancellationToken)
    {
        // The same lock the checkout takes before spending the balance; see
        // CheckoutRepository for why SQLite is left out of it.
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM customers WHERE "Id" = {customerId} FOR UPDATE""",
                cancellationToken);
        }

        return await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
    }

    public void AddNotification(CustomerNotification notification) => db.CustomerNotifications.Add(notification);

    public Task<Order?> FindOrderAsync(Guid customerId, string idOrNumber, CancellationToken cancellationToken)
    {
        var query = db.Orders.Include(o => o.Lines).Where(o => o.CustomerId == customerId);

        query = Guid.TryParse(idOrNumber, out var id)
            ? query.Where(o => o.Id == id)
            : query.Where(o => o.Number == idOrNumber);

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public void AddReturnRequest(ReturnRequest request) => db.ReturnRequests.Add(request);

    /// <summary>
    /// Delivered is the bar, not merely ordered: a "verified purchase" badge on
    /// a review of something still in transit would be a claim the shop cannot
    /// back.
    /// </summary>
    public Task<bool> HasPurchasedAsync(Guid customerId, Guid productId, CancellationToken cancellationToken) =>
        db.Orders.AnyAsync(
            o => o.CustomerId == customerId
                && o.Status == OrderStatus.Delivered
                && o.Lines.Any(l => l.ProductId == productId),
            cancellationToken);

    public Task<bool> HasReviewedAsync(Guid customerId, Guid productId, CancellationToken cancellationToken) =>
        db.ProductReviews.AnyAsync(r => r.CustomerId == customerId && r.ProductId == productId, cancellationToken);

    public void AddReview(ProductReview review) => db.ProductReviews.Add(review);

    public void AddQuestion(ProductQuestion question) => db.ProductQuestions.Add(question);
}
