using Bojan.Application.Contracts;
using Bojan.Domain.Customers;
using Bojan.Domain.Orders;
using Bojan.Domain.Reviews;

namespace Bojan.Application.Accounts;

/// <summary>
/// Everything the signed-in customer's screens read.
/// </summary>
/// <remarks>
/// Every method takes the customer id as its first argument, and the
/// implementation filters on it in the query rather than checking afterwards.
/// That is the whole ownership story for Phase 3: there is no code path that
/// can read another customer's row and then decide whether to return it, so
/// "must 404, not 403" (<c>BACKEND.md</c> Phase 3) falls out of the query
/// returning nothing rather than out of a check someone has to remember.
/// </remarks>
public interface IAccountQueries
{
    Task<UserDto?> GetProfileAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderSummaryDto>> ListOrdersAsync(Guid customerId, string? status, CancellationToken cancellationToken);

    /// <summary>Accepts the order's id or its human-readable number — the frontend passes whichever it has.</summary>
    Task<OrderDetailDto?> GetOrderAsync(Guid customerId, string idOrNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<AddressDto>> ListAddressesAsync(Guid customerId, CancellationToken cancellationToken);

    Task<AddressDto?> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductDto>> ListWishlistAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReturnRequestDto>> ListReturnsAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Accepts the request's id or its <c>RT-…</c> code, for the same reason as orders.</summary>
    Task<ReturnRequestDto?> GetReturnAsync(Guid customerId, string idOrCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationDto>> ListNotificationsAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SupportTicketDto>> ListTicketsAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MyReviewDto>> ListMyReviewsAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Delivered items the customer has not reviewed yet — screen 55.</summary>
    Task<IReadOnlyList<AwaitingReviewDto>> ListAwaitingReviewsAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WalletTransactionDto>> ListWalletTransactionsAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CouponDto>> ListCouponsAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductDto>> ListRecentlyViewedAsync(Guid customerId, CancellationToken cancellationToken);
}

/// <summary>
/// The customer's own writes.
/// </summary>
/// <remarks>
/// Ownership again lives in the lookup: <see cref="FindAddressAsync"/> takes
/// the customer id, so a caller cannot hold an <see cref="Address"/> belonging
/// to someone else in the first place.
/// </remarks>
public interface IAccountRepository
{
    Task<Customer?> FindAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Loads the customer with their addresses attached, for a write that touches both.</summary>
    Task<Customer?> FindWithAddressesAsync(Guid customerId, CancellationToken cancellationToken);

    Task<Address?> FindAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken);

    /// <summary>
    /// Tracks a new address as an insert.
    /// </summary>
    /// <remarks>
    /// Needed even though <see cref="Customer.AddAddress"/> already puts it in
    /// the collection: EF decides the state of an entity discovered through a
    /// tracked parent's navigation by whether its key is already set, and
    /// <see cref="Domain.Common.Entity.Id"/> assigns a GUID at construction. A
    /// set key reads as "existing", so the address would be saved as an UPDATE
    /// of a row that is not there. Adding it explicitly says which it is.
    /// </remarks>
    void AddAddress(Address address);

    void RemoveAddress(Address address);

    Task<IReadOnlyList<CustomerNotification>> FindNotificationsAsync(
        Guid customerId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    Task<WishlistItem?> FindWishlistItemAsync(Guid customerId, Guid productId, CancellationToken cancellationToken);

    void AddWishlistItem(WishlistItem item);

    void RemoveWishlistItem(WishlistItem item);

    Task<int> ClearSearchHistoryAsync(Guid customerId, CancellationToken cancellationToken);

    void AddWalletTransaction(WalletTransaction transaction);

    void AddWalletTopUp(WalletTopUp topUp);

    /// <summary>This customer's top-ups still awaiting a decision, oldest first.</summary>
    Task<IReadOnlyList<WalletTopUp>> ListPendingTopUpsAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// A pending top-up of this customer's, by gateway reference. Untracked, and
    /// not locked — the peek before the gateway is asked, never the instance a
    /// decision is written to.
    /// </summary>
    /// <remarks>
    /// Scoped to the customer deliberately: a reference is a bearer string, and
    /// looking one up without asking whose it is would let a signed-in shopper
    /// settle a stranger's top-up into their own wallet by quoting it.
    /// </remarks>
    Task<WalletTopUp?> FindTopUpByReferenceAsync(
        Guid customerId,
        string gatewayReference,
        CancellationToken cancellationToken);

    /// <summary>
    /// The top-up with its own row locked, for the read whose status decides
    /// whether money moves.
    /// </summary>
    /// <remarks>
    /// Must be called inside a transaction — a <c>FOR UPDATE</c> taken in
    /// autocommit is released by the very next statement, which makes it a
    /// comment rather than a lock.
    /// </remarks>
    Task<WalletTopUp?> FindTopUpForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The ledger row a top-up owns, so a decision can move it off Pending.</summary>
    Task<WalletTransaction?> FindWalletTransactionAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The customer with their row locked, for a write that reads the balance
    /// and then changes it.
    /// </summary>
    /// <remarks>
    /// Crediting is not exempt from the lock that spending needs: without it two
    /// writers read the old balance and both write old + amount, and one of the
    /// two credits is lost.
    /// <para>
    /// This lock guards the arithmetic, not the decision. It cannot tell a
    /// caller that another has already approved the same top-up — it serialises
    /// them and lets both proceed. Idempotence comes from
    /// <see cref="FindTopUpForUpdateAsync"/> locking the top-up row itself, so
    /// that the status <see cref="WalletTopUp.Approve"/> reads is the status
    /// after the racer committed.
    /// </para>
    /// </remarks>
    Task<Customer?> FindForUpdateAsync(Guid customerId, CancellationToken cancellationToken);

    void AddNotification(CustomerNotification notification);

    Task<Order?> FindOrderAsync(Guid customerId, string idOrNumber, CancellationToken cancellationToken);

    void AddReturnRequest(ReturnRequest request);

    /// <summary>
    /// How much of each product this order already has outstanding return
    /// claims for, keyed by product.
    /// </summary>
    /// <remarks>
    /// A return is checked against the order line it names, and that check is
    /// per request — so two of them, each for the whole quantity, both passed,
    /// and the shop was asked to take back twice what it sold. Rejected claims
    /// are excluded: refusing one has to give its quantity back, or a mistaken
    /// request would bar the customer from ever filing a correct one.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, int>> GetClaimedReturnQuantitiesAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    /// <summary>True when the customer has a delivered order containing this product — the "verified purchase" badge.</summary>
    Task<bool> HasPurchasedAsync(Guid customerId, Guid productId, CancellationToken cancellationToken);

    Task<bool> HasReviewedAsync(Guid customerId, Guid productId, CancellationToken cancellationToken);

    void AddReview(ProductReview review);

    void AddQuestion(ProductQuestion question);
}
