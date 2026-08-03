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

    /// <inheritdoc cref="IAccountRepository.FindTopUpByReferenceAsync"/>
    public Task<WalletTopUp?> FindTopUpByReferenceAsync(
        Guid customerId,
        string gatewayReference,
        CancellationToken cancellationToken) =>
        db.WalletTopUps.FirstOrDefaultAsync(
            t => t.CustomerId == customerId && t.GatewayReference == gatewayReference,
            cancellationToken);

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
