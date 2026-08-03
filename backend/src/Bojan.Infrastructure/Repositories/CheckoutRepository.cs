using Bojan.Application.Checkout;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Customers;
using Bojan.Domain.Orders;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Repositories;

/// <summary>Phase 4's data access — the money path's reads and writes.</summary>
public sealed class CheckoutRepository(BojanDbContext db) : ICheckoutRepository
{
    /// <summary>
    /// Loads the basket's products with a row lock held until the transaction
    /// commits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>FOR UPDATE</c> is the whole point: two orders for the last unit must
    /// not both read <c>stock = 1</c>, decide there is enough, and both
    /// succeed. The lock serialises them, so the second one re-reads
    /// <c>stock = 0</c> and is refused.
    /// </para>
    /// <para>
    /// Only PostgreSQL is asked for the lock. SQLite — which
    /// <c>Bojan.Api.Tests</c> runs against — has no <c>FOR UPDATE</c> syntax
    /// and does not need one: it serialises writers at the database level
    /// anyway. Emitting the hint unconditionally would break every endpoint
    /// test for a guarantee the test database already provides.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<Product>> LoadProductsForUpdateAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToList();

        if (db.Database.IsNpgsql())
        {
            // The lock is taken by its own statement, and the rows are then
            // loaded through EF as usual. Two reasons: the raw SQL never has to
            // agree with the entity's column mapping — it names only a table and
            // a key — and `ORDER BY` makes concurrent orders take their locks in
            // the same sequence, so two baskets sharing two products cannot
            // deadlock each other. The lock is held by the transaction until it
            // commits, whichever statement took it.
            //
            // Identifiers are quoted because columns are PascalCase in this
            // schema while table names are lower_snake; unquoted `Id` would fold
            // to `id` and not exist.
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM products WHERE "Id" = ANY({ids}) ORDER BY "Id" FOR UPDATE""",
                cancellationToken);
        }

        return await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> LoadProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToList();
        return await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc cref="ICheckoutRepository.LoadSkusForUpdateAsync"/>
    public async Task<IReadOnlyList<ProductSku>> LoadSkusForUpdateAsync(
        IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken)
    {
        var ids = skuIds.Distinct().ToList();

        if (ids.Count > 0 && db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM product_skus WHERE "Id" = ANY({ids}) ORDER BY "Id" FOR UPDATE""",
                cancellationToken);
        }

        return await db.ProductSkus.Where(s => ids.Contains(s.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductSku>> LoadSkusAsync(
        IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken)
    {
        var ids = skuIds.Distinct().ToList();
        return await db.ProductSkus.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(cancellationToken);
    }

    public Task<Address?> FindAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken) =>
        db.Addresses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, cancellationToken);

    public Task<ShippingMethod?> FindShippingMethodAsync(string code, CancellationToken cancellationToken) =>
        db.ShippingMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Code == code && m.IsActive, cancellationToken);

    public Task<PaymentMethod?> FindPaymentMethodAsync(string code, CancellationToken cancellationToken) =>
        db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Code == code && m.IsActive, cancellationToken);

    public async Task<IReadOnlyList<ShippingMethod>> ListShippingMethodsAsync(CancellationToken cancellationToken) =>
        await db.ShippingMethods.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentMethod>> ListPaymentMethodsAsync(CancellationToken cancellationToken) =>
        await db.PaymentMethods.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

    public Task<Coupon?> FindCouponAsync(string code, CancellationToken cancellationToken) =>
        db.Coupons.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);

    /// <inheritdoc cref="ICheckoutRepository.FindCouponForUpdateAsync"/>
    public async Task<Coupon?> FindCouponForUpdateAsync(string code, CancellationToken cancellationToken)
    {
        // Locked on the code, which is what the caller has and what the unique
        // index is on. Same PostgreSQL-only reasoning as the product lock above.
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM coupons WHERE "Code" = {code} FOR UPDATE""",
                cancellationToken);
        }

        return await db.Coupons.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
    }

    /// <summary>
    /// Counted from the orders that actually used the code, not from a
    /// redemption table: the order is the record of the redemption, and a
    /// second table could disagree with it.
    /// </summary>
    public Task<int> CountCustomerRedemptionsAsync(Guid customerId, Guid couponId, CancellationToken cancellationToken) =>
        (from order in db.Orders.AsNoTracking()
         join coupon in db.Coupons.AsNoTracking() on order.CouponCode equals coupon.Code
         where order.CustomerId == customerId
             && coupon.Id == couponId
             && order.Status != OrderStatus.Cancelled
         select order.Id)
        .CountAsync(cancellationToken);

    public Task<Order?> FindByIdempotencyKeyAsync(
        Guid customerId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.IdempotencyKey == idempotencyKey, cancellationToken);

    /// <inheritdoc cref="ICheckoutRepository.FindCustomerForUpdateAsync"/>
    public async Task<Customer?> FindCustomerForUpdateAsync(Guid customerId, CancellationToken cancellationToken)
    {
        // Same shape, and the same reasoning, as the product lock above: the
        // statement names only the table and the key, and SQLite is left alone
        // because it serialises writers itself.
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM customers WHERE "Id" = {customerId} FOR UPDATE""",
                cancellationToken);
        }

        return await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
    }

    public void AddOrder(Order order) => db.Orders.Add(order);

    public void AddWalletTransaction(WalletTransaction transaction) => db.WalletTransactions.Add(transaction);

    /// <summary>
    /// The public tracking lookup.
    /// </summary>
    /// <remarks>
    /// Matched on the order number and the customer's phone together. A number
    /// alone would make this an enumeration oracle: order numbers are short
    /// enough to walk, which is also why
    /// <see cref="OrderNumber.NewOrderNumber"/> generates them randomly rather
    /// than in sequence.
    /// </remarks>
    public async Task<OrderSummaryDto?> TrackAsync(string number, string phone, CancellationToken cancellationToken)
    {
        var row = await (
            from order in db.Orders.AsNoTracking()
            join customer in db.Customers.AsNoTracking() on order.CustomerId equals customer.Id
            where order.Number == number && customer.Phone == phone
            select new
            {
                order.Id,
                order.Number,
                order.PlacedAtUtc,
                order.Status,
                ItemCount = order.Lines.Count,
                order.Subtotal,
                order.Discount,
                order.Shipping,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new OrderSummaryDto(
                row.Id.ToString(),
                row.Number,
                row.PlacedAtUtc,
                WireFormat.StorefrontOrderStatus(row.Status),
                row.ItemCount,
                (row.Subtotal.ClampedMinus(row.Discount) + row.Shipping).Amount,
                null);
    }
}
