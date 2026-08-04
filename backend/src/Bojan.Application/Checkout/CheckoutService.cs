using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
using Bojan.Domain.Customers;
using Bojan.Domain.Orders;

namespace Bojan.Application.Checkout;

/// <summary>
/// The money path.
/// </summary>
/// <remarks>
/// <para>
/// Every rule <c>BACKEND.md</c> Phase 4 lists is enforced here, and each is
/// enforced because the basket arrives from the shopper's own browser: the
/// frontend checks the same things at its edge
/// (<c>apps/storefront/src/app/api/orders/route.ts</c>), but that check runs on
/// a machine an attacker can skip past by calling this API directly.
/// </para>
/// <list type="number">
/// <item>Every line is re-priced from the database.</item>
/// <item>Stock is re-checked and reserved inside the transaction.</item>
/// <item>The address must belong to the caller.</item>
/// <item>Both method ids must exist and be active.</item>
/// <item>The coupon is re-validated — activity, minimum spend, expiry, and
/// per-customer use.</item>
/// <item>Discount is clamped to the goods total; an empty basket is refused.</item>
/// <item>An <c>Idempotency-Key</c> that has been seen returns the original
/// order rather than placing a second one.</item>
/// </list>
/// </remarks>
public sealed class CheckoutService(
    ICheckoutRepository repository,
    IUnitOfWork unitOfWork,
    IPaymentGateway gateway,
    IDateTimeProvider clock)
{
    /// <summary>Same ceiling the frontend's own order route applies, so the two layers cannot disagree.</summary>
    private const int MaxLines = 50;
    private const int MaxQuantityPerLine = 20;

    /// <summary>
    /// Collapses repeats of the same product into one line.
    /// </summary>
    /// <remarks>
    /// Nothing downstream is safe against a basket that names one product
    /// twice. Every per-line check — the quantity ceiling, and the stock test
    /// in <see cref="PriceLines"/> — compares a single line against the whole
    /// of stock, so two lines of twenty pass independently against a stock of
    /// thirty and then <c>ReduceStock</c> throws on the second decrement: a 500
    /// where the shopper should have been told the item is short. Where stock
    /// is sufficient the same basket quietly buys forty of something capped at
    /// twenty. Summing first makes both checks see the quantity actually being
    /// ordered, which is the only quantity that means anything.
    /// </remarks>
    private static List<OrderLineRequest> Consolidate(IReadOnlyList<OrderLineRequest> lines) =>
    [
        .. lines
            .GroupBy(line => line.ProductId)
            .Select(group => new OrderLineRequest(group.Key, group.Sum(line => line.Quantity))),
    ];

    public async Task<IReadOnlyList<ShippingMethodDto>> ListShippingMethodsAsync(CancellationToken cancellationToken)
    {
        var methods = await repository.ListShippingMethodsAsync(cancellationToken);
        return [.. methods.Select(m => new ShippingMethodDto(m.Code, m.Title, m.Price.Amount, m.Estimate, m.Icon))];
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> ListPaymentMethodsAsync(CancellationToken cancellationToken)
    {
        var methods = await repository.ListPaymentMethodsAsync(cancellationToken);
        return [.. methods.Select(m => new PaymentMethodDto(m.Code, m.Title, m.Note, m.Icon, m.RequiresGateway))];
    }

    /// <summary>
    /// <c>POST /cart/coupon</c>.
    /// </summary>
    /// <remarks>
    /// The subtotal is computed from the submitted lines at database prices,
    /// not taken from the request: the discount this returns is what the
    /// checkout subtracts, so accepting a client-supplied subtotal would let
    /// anyone claim a percentage of an imaginary basket. An invalid code is a
    /// non-2xx, never a <c>{ valid: false }</c> body — <c>BACKEND.md</c>
    /// Phase 4 is explicit, and the frontend's <c>ApiError</c> path depends
    /// on it.
    /// </remarks>
    public async Task<UseCaseResult<CouponResultDto>> ValidateCouponAsync(
        Guid customerId,
        string code,
        IReadOnlyList<OrderLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var normalised = code.Trim().ToUpperInvariant();

        var coupon = await repository.FindCouponAsync(normalised, cancellationToken);
        if (coupon is null)
        {
            return UseCaseResult<CouponResultDto>.Failure(UseCaseError.CouponRejected, "unknown");
        }

        // Consolidated for the same reason the order path is: the subtotal this
        // discount is calculated against has to be the one the order will
        // actually charge, and a basket naming a product twice prices to a
        // different number here than it would there.
        var consolidated = Consolidate(lines);

        var products = await repository.LoadProductsAsync(
            [.. consolidated.Select(line => line.ProductId)], cancellationToken);
        var priced = PriceLines(consolidated, products);
        if (priced.Error is { } lineError)
        {
            return UseCaseResult<CouponResultDto>.Failure(lineError, priced.Detail);
        }

        var redemptions = await repository.CountCustomerRedemptionsAsync(customerId, coupon.Id, cancellationToken);
        if (redemptions > 0)
        {
            return UseCaseResult<CouponResultDto>.Failure(UseCaseError.CouponRejected, "already-used");
        }

        try
        {
            var discount = coupon.Validate(priced.Subtotal, clock.UtcNow);
            return new CouponResultDto(coupon.Code, discount.Amount);
        }
        catch (InvalidOperationException)
        {
            // Coupon.Validate throws for every "does not apply" reason. The
            // caller gets one rejection either way — telling them *which* rule
            // they missed would let a script map out the coupon's terms.
            return UseCaseResult<CouponResultDto>.Failure(UseCaseError.CouponRejected, "not-applicable");
        }
    }

    /// <summary>
    /// <c>POST /orders</c>. Everything happens in one transaction so a
    /// half-placed order cannot exist.
    /// </summary>
    public async Task<UseCaseResult<PlacedOrderDto>> PlaceOrderAsync(
        Guid customerId,
        PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Lines.Count is 0 or > MaxLines)
        {
            return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.Invalid, "lines");
        }

        // Bounded before anything is summed: fifty lines of int.MaxValue would
        // overflow the consolidation below and wrap into a quantity that passes
        // every check after it.
        if (request.Lines.Any(line => line.Quantity is < 1 or > MaxQuantityPerLine))
        {
            return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.Invalid, "quantity");
        }

        var lines = Consolidate(request.Lines);

        // Re-applied to the consolidated quantity, which is the one the shopper
        // is actually buying — the per-line ceiling means nothing if the same
        // product may appear on as many lines as the basket has room for.
        if (lines.Any(line => line.Quantity > MaxQuantityPerLine))
        {
            return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.Invalid, "quantity");
        }

        request = request with { Lines = lines };

        // Checked before the transaction opens: a repeat submission is the
        // common case for a shopper who double-tapped, and it should not take
        // a write lock to answer.
        var existing = await repository.FindByIdempotencyKeyAsync(customerId, request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return new PlacedOrderDto(existing.Number, existing.PaymentUrl);
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            token => PlaceOrderCoreAsync(customerId, request, token),
            cancellationToken);
    }

    private async Task<UseCaseResult<PlacedOrderDto>> PlaceOrderCoreAsync(
        Guid customerId,
        PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        // Re-checked inside the transaction: two requests carrying the same key
        // can both pass the check above before either has written anything.
        var duplicate = await repository.FindByIdempotencyKeyAsync(customerId, request.IdempotencyKey, cancellationToken);
        if (duplicate is not null)
        {
            return new PlacedOrderDto(duplicate.Number, duplicate.PaymentUrl);
        }

        var address = await repository.FindAddressAsync(customerId, request.AddressId, cancellationToken);
        if (address is null)
        {
            // Rule 3. Not-found rather than forbidden: an address that exists
            // but belongs to someone else must be indistinguishable from one
            // that does not exist.
            return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.Invalid, "address");
        }

        var shipping = await repository.FindShippingMethodAsync(request.ShippingMethodId, cancellationToken);
        if (shipping is null)
        {
            return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.Invalid, "shipping-method");
        }

        var payment = await repository.FindPaymentMethodAsync(request.PaymentMethodId, cancellationToken);
        if (payment is null)
        {
            return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.Invalid, "payment-method");
        }

        // Locked for update — this is what stops two orders for the last unit
        // from both reading stock 1.
        var products = await repository.LoadProductsForUpdateAsync(
            [.. request.Lines.Select(line => line.ProductId)], cancellationToken);

        var priced = PriceLines(request.Lines, products);
        if (priced.Error is { } lineError)
        {
            return UseCaseResult<PlacedOrderDto>.Failure(lineError, priced.Detail);
        }

        var discount = Money.Zero;
        Coupon? coupon = null;

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            coupon = await repository.FindCouponAsync(request.CouponCode.Trim().ToUpperInvariant(), cancellationToken);
            if (coupon is null)
            {
                return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.CouponRejected, "unknown");
            }

            if (await repository.CountCustomerRedemptionsAsync(customerId, coupon.Id, cancellationToken) > 0)
            {
                return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.CouponRejected, "already-used");
            }

            try
            {
                discount = coupon.Validate(priced.Subtotal, clock.UtcNow);
            }
            catch (InvalidOperationException)
            {
                return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.CouponRejected, "not-applicable");
            }
        }

        var customer = await repository.FindCustomerAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.Unauthorized);
        }

        var payable = priced.Subtotal.ClampedMinus(discount) + shipping.Price;

        if (payment.UsesWallet && customer.WalletBalance < payable)
        {
            return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.Invalid, "wallet-balance");
        }

        var number = OrderNumber.NewOrderNumber();

        var order = Order.Create(
            number,
            customerId,
            priced.Lines,
            address.Id,
            FormatAddress(address),
            shipping.Title,
            payment.Title,
            priced.Subtotal,
            discount,
            shipping.Price,
            request.IdempotencyKey,
            coupon?.Code,
            request.Note);

        // Rule 2 — reserved, not merely checked. The rows are locked, so this
        // decrement is safe against a concurrent order for the same product.
        foreach (var line in request.Lines)
        {
            products.First(product => product.Id == line.ProductId).ReduceStock(line.Quantity);
        }

        coupon?.RecordRedemption();

        if (payment.UsesWallet)
        {
            customer.DebitWallet(payable);
            repository.AddWalletTransaction(new WalletTransaction
            {
                CustomerId = customerId,
                Title = $"پرداخت سفارش {number}",
                Amount = -payable.Amount,
                Status = WalletTransactionStatus.Success,
                Icon = "shopping_bag",
                CreatedAtUtc = clock.UtcNow,
            });
        }

        if (payment.RequiresGateway)
        {
            var session = await gateway.StartAsync(number, payable.Amount, cancellationToken);
            order.PaymentUrl = session.PaymentUrl;
        }

        repository.AddOrder(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Absent for cash on delivery: the checkout redirects whenever this is
        // present, so returning a URL here would send the shopper to a payment
        // page for money they are meant to hand over at the door.
        return new PlacedOrderDto(order.Number, order.PaymentUrl);
    }

    /// <summary>
    /// <c>GET /orders/track</c> — the public lookup. Matching on number and
    /// phone together is what stops it being an order-number enumeration
    /// vector.
    /// </summary>
    public Task<OrderSummaryDto?> TrackAsync(string number, string phone, CancellationToken cancellationToken) =>
        repository.TrackAsync(number.Trim(), phone.Trim(), cancellationToken);

    private sealed record PricedBasket(
        IReadOnlyCollection<OrderLineDraft> Lines,
        Money Subtotal,
        UseCaseError? Error,
        string? Detail);

    /// <summary>
    /// Rule 1: every line priced from the database row, never from anything
    /// the client sent — the request has no price field at all, by design.
    /// </summary>
    private static PricedBasket PriceLines(IReadOnlyList<OrderLineRequest> requested, IReadOnlyList<Product> products)
    {
        var lines = new List<OrderLineDraft>(requested.Count);
        var subtotal = Money.Zero;

        foreach (var line in requested)
        {
            var product = products.FirstOrDefault(candidate => candidate.Id == line.ProductId);

            if (product is null || product.IsDeleted || !product.IsPublished)
            {
                return new PricedBasket([], Money.Zero, UseCaseError.Invalid, "unknown-product");
            }

            // A product whose stock is not counted, or that is sold on
            // backorder, has nothing to be short of — those two flags are set
            // per product on the panel's own form, and refusing the order here
            // regardless would make both of them labels.
            if (product.RequiresStockOnHand && product.Stock < line.Quantity)
            {
                return new PricedBasket([], Money.Zero, UseCaseError.OutOfStock, product.Slug);
            }

            lines.Add(new OrderLineDraft(
                product.Id,
                product.Slug,
                product.Title,
                product.ImageUrl,
                line.Quantity,
                product.Price));

            subtotal += product.Price * line.Quantity;
        }

        return new PricedBasket(lines, subtotal, null, null);
    }

    /// <summary>Flattens an address into the single line the order screens render.</summary>
    private static string FormatAddress(Address address) =>
        $"{address.Province}، {address.City}، {address.Line} — کد پستی {address.PostalCode}";
}
