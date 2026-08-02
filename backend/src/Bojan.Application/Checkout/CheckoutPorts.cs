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

    Task<Coupon?> FindCouponAsync(string code, CancellationToken cancellationToken);

    /// <summary>How many times this customer has already redeemed this coupon — per-customer use, Phase 4 rule 5.</summary>
    Task<int> CountCustomerRedemptionsAsync(Guid customerId, Guid couponId, CancellationToken cancellationToken);

    Task<Order?> FindByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken);

    Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken cancellationToken);

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
