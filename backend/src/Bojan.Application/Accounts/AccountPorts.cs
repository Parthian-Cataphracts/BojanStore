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

    /// <summary>
    /// Screen 34 — the invoice for one of this customer's orders.
    /// </summary>
    /// <remarks>
    /// Null both when the order is not this customer's and when it has not been
    /// delivered. The two are not distinguished here for the reason the rest of
    /// this interface takes a customer id: a caller that can tell "not yours"
    /// from "no invoice yet" can use the endpoint to learn which order numbers
    /// exist.
    /// </remarks>
    Task<InvoiceDto?> GetInvoiceAsync(Guid customerId, string idOrNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<AddressDto>> ListAddressesAsync(Guid customerId, CancellationToken cancellationToken);

    Task<AddressDto?> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductDto>> ListWishlistAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReturnRequestDto>> ListReturnsAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Accepts the request's id or its <c>RT-…</c> code, for the same reason as orders.</summary>
    Task<ReturnRequestDto?> GetReturnAsync(Guid customerId, string idOrCode, CancellationToken cancellationToken);

    /// <summary>
    /// Screen 53's feed, newest first, capped at <paramref name="limit"/>.
    /// </summary>
    /// <remarks>
    /// Capped rather than paged: the screen is a reverse-chronological list
    /// with a kind filter and no pager in the design, and an older notification
    /// is of no interest — it is not a record of anything, the order and the
    /// ticket it points at are. A page control here would be scaffolding for a
    /// journey nobody makes.
    /// </remarks>
    Task<IReadOnlyList<NotificationDto>> ListNotificationsAsync(
        Guid customerId,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Unread count for the header bell.</summary>
    Task<int> CountUnreadNotificationsAsync(Guid customerId, CancellationToken cancellationToken);

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

    /// <summary>
    /// The same lookup with the order row locked, for filing a return.
    /// </summary>
    /// <remarks>
    /// Reading the order, counting what earlier requests already claimed and
    /// writing the new one were three separate steps with nothing holding them
    /// together. Two requests filed at the same moment both read the same
    /// remaining quantity, both passed the check, and both were written — a
    /// customer could ask the shop to take back more than it sold, and each
    /// request looked correct on its own. The lock is on the order because that
    /// is what both are counting against.
    /// </remarks>
    Task<Order?> FindOrderForUpdateAsync(Guid customerId, string idOrNumber, CancellationToken cancellationToken);

    void AddReturnRequest(ReturnRequest request);

    /// <summary>
    /// How much this order already has outstanding return claims for, keyed by
    /// the product and the combination — the same pair that identifies an order
    /// line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A return is checked against the order line it names, and that check is
    /// per request — so two of them, each for the whole quantity, both passed,
    /// and the shop was asked to take back twice what it sold. Rejected claims
    /// are excluded: refusing one has to give its quantity back, or a mistaken
    /// request would bar the customer from ever filing a correct one.
    /// </para>
    /// <para>
    /// Keyed on the pair rather than on the product alone. An order can hold two
    /// lines of one product in different variants, and merging them meant
    /// returning two of the red exhausted the blue's allowance as well.
    /// </para>
    /// </remarks>
    Task<IReadOnlyDictionary<(Guid ProductId, Guid? SkuId), int>> GetClaimedReturnQuantitiesAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    /// <summary>True when the customer has a delivered order containing this product — the "verified purchase" badge.</summary>
    Task<bool> HasPurchasedAsync(Guid customerId, Guid productId, CancellationToken cancellationToken);

    Task<bool> HasReviewedAsync(Guid customerId, Guid productId, CancellationToken cancellationToken);

    void AddReview(ProductReview review);

    void AddQuestion(ProductQuestion question);
}
