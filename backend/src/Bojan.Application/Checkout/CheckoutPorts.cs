using Bojan.Application.Contracts;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Customers;
using Bojan.Domain.Orders;

namespace Bojan.Application.Checkout;

/// <summary>
/// What order placement needs, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LoadProductsForUpdateAsync"/> is the load-bearing one. It takes
/// a row lock on each product so two orders for the last unit cannot both read
/// stock 1 and both succeed — the classic oversell. Called inside
/// <c>IUnitOfWork.ExecuteInTransactionAsync</c>, never outside it.
/// </para>
/// <para>
/// <see cref="FindByIdempotencyKeyAsync"/> backs <c>BACKEND.md</c> Phase 4
/// rule 7: "A double-submitted order is the single worst bug this system can
/// have." A repeat of the same key returns the order that already exists
/// rather than creating a second one.
/// </para>
/// </remarks>
public interface ICheckoutRepository
{
    /// <summary>
    /// The loyalty club's tiers, or empty when the shop runs no club.
    /// </summary>
    /// <remarks>
    /// Read on every checkout because a member's standing discount is part of
    /// the price. Three rows at most, and the query is keyless — see the
    /// storefront's own read for the copy shoppers are shown.
    /// </remarks>
    Task<IReadOnlyList<LoyaltyTier>> ListLoyaltyTiersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> LoadProductsForUpdateAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);

    /// <summary>Prices a basket without locking anything — for the coupon check, which places no order.</summary>
    Task<IReadOnlyList<Product>> LoadProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);

    /// <summary>Same locking guarantee as <see cref="LoadProductsForUpdateAsync"/>, for the SKUs a basket names.</summary>
    Task<IReadOnlyList<ProductSku>> LoadSkusForUpdateAsync(
        IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken);

    /// <summary>Unlocked SKU read — for the coupon check, which places no order.</summary>
    Task<IReadOnlyList<ProductSku>> LoadSkusAsync(
        IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken);

    Task<Address?> FindAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken);

    Task<ShippingMethod?> FindShippingMethodAsync(string code, CancellationToken cancellationToken);

    Task<PaymentMethod?> FindPaymentMethodAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShippingMethod>> ListShippingMethodsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentMethod>> ListPaymentMethodsAsync(CancellationToken cancellationToken);

    /// <summary>The coupon, unlocked — for the preview that only reports whether a code would apply.</summary>
    Task<Coupon?> FindCouponAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// The coupon with its row locked, for the placement that is about to
    /// consume a redemption.
    /// </summary>
    /// <remarks>
    /// <c>Coupon.Validate</c> reads <c>RedemptionCount</c> against
    /// <c>MaxRedemptions</c> and <c>RecordRedemption</c> then increments it —
    /// a read-modify-write, and EF writes the count as an absolute value rather
    /// than an increment. Unlocked, two final redemptions of a limited code both
    /// read one-below-the-limit, both pass, and both write the same number: the
    /// code is redeemed once more than it allows and the counter does not even
    /// show it. The lock also covers the per-customer check, which is made
    /// before the customer row is locked and so has no protection of its own.
    /// </remarks>
    Task<Coupon?> FindCouponForUpdateAsync(string code, CancellationToken cancellationToken);

    /// <summary>How many times this customer has already redeemed this coupon — per-customer use, Phase 4 rule 5.</summary>
    Task<int> CountCustomerRedemptionsAsync(Guid customerId, Guid couponId, CancellationToken cancellationToken);

    Task<Order?> FindByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// The shopper placing the order, with their row locked for the rest of the
    /// transaction.
    /// </summary>
    /// <remarks>
    /// The same guarantee <see cref="LoadProductsForUpdateAsync"/> gives stock,
    /// and needed for the same reason. Wallet balance is stock: two orders
    /// placed at once would otherwise both read the same balance, both find it
    /// sufficient, and both spend it — the second write simply overwriting the
    /// first, leaving the customer having bought twice what they paid for. This
    /// became reachable the moment the wallet could pay part of an order rather
    /// than only all of it, because partial payment is what makes using the
    /// wallet on most orders possible at all.
    /// </remarks>
    Task<Customer?> FindCustomerForUpdateAsync(Guid customerId, CancellationToken cancellationToken);

    void AddOrder(Order order);

    void AddWalletTransaction(WalletTransaction transaction);

    /// <summary>
    /// The public order lookup behind <c>GET /orders/track</c>. Matches on
    /// number <em>and</em> phone together — <c>BACKEND.md</c> Phase 4 is
    /// explicit that a number alone must never return anything, because that
    /// makes the endpoint an order-number enumeration vector.
    /// </summary>
    Task<OrderSummaryDto?> TrackAsync(string number, string phone, CancellationToken cancellationToken);
}

/// <summary>
/// One line of a submitted basket: a product, an optional chosen SKU, and a
/// count — never a price.
/// </summary>
/// <remarks>
/// <see cref="SkuId"/> is null for a product with no variants (screen 108),
/// which still prices and reserves from <c>Product</c> itself.
/// </remarks>
public sealed record OrderLineRequest(Guid ProductId, int Quantity, Guid? SkuId = null);

/// <summary>
/// <c>POST /orders</c>'s body, exactly as
/// <c>apps/storefront/src/lib/api/cart.ts</c> declares it.
/// </summary>
/// <remarks>
/// Note what is absent: no prices and no totals. The basket comes from the
/// shopper's browser, so the frontend deliberately sends only ids and
/// quantities and expects the API to price it. <c>BACKEND.md</c> Phase 4: "Do
/// not add prices to this contract."
/// </remarks>
public sealed record PlaceOrderRequest(
    IReadOnlyList<OrderLineRequest> Lines,
    Guid AddressId,
    string ShippingMethodId,
    string PaymentMethodId,
    string? CouponCode,
    string? Note,
    string IdempotencyKey,
    /// <summary>
    /// Screen 74's chosen day and slot, already formatted. A preference the
    /// order records rather than a promise it schedules against.
    /// </summary>
    string? DeliveryWindow = null);

/// <summary>
/// What the loyalty club costs to earn into.
/// </summary>
/// <remarks>
/// Its own port rather than a field on the tiers, because it is one figure for
/// the shop rather than a property of any rung — and because the two are edited
/// on the same screen but read by different callers: the checkout wants the
/// tiers, and only delivery wants the rate.
/// </remarks>
public interface ILoyaltySettings
{
    /// <summary>
    /// Toman a member must spend to earn one point. Zero or less earns nothing,
    /// which is how the owner pauses the club without deleting anyone's balance.
    /// </summary>
    Task<int> TomanPerPointAsync(CancellationToken cancellationToken);
}
