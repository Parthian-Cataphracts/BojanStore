using Bojan.Application.Common;
using Bojan.Application.Notifications;
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
    Payments.IPaymentSettlementRepository settlements,
    IUnitOfWork unitOfWork,
    IPaymentGateway gateway,
    ICustomerMailer mailer,
    EmailTemplates templates,
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
            .GroupBy(line => (line.ProductId, line.SkuId))
            .Select(group => new OrderLineRequest(group.Key.ProductId, group.Sum(line => line.Quantity), group.Key.SkuId)),
    ];

    public async Task<IReadOnlyList<ShippingMethodDto>> ListShippingMethodsAsync(CancellationToken cancellationToken)
    {
        var methods = await repository.ListShippingMethodsAsync(cancellationToken);
        return [.. methods.Select(m =>
            new ShippingMethodDto(m.Code, m.Title, m.Price.Amount, m.Estimate, m.Icon, m.FreeAboveAmount))];
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> ListPaymentMethodsAsync(CancellationToken cancellationToken)
    {
        var methods = await repository.ListPaymentMethodsAsync(cancellationToken);
        return [.. methods.Select(m => new PaymentMethodDto(
            m.Code, m.Title, m.Note, m.Icon, m.RequiresGateway, m.UsesWallet))];
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
        var skuIds = consolidated.Where(line => line.SkuId.HasValue).Select(line => line.SkuId!.Value).ToList();
        IReadOnlyList<ProductSku> skus = skuIds.Count > 0
            ? await repository.LoadSkusAsync(skuIds, cancellationToken)
            : [];
        var priced = PriceLines(consolidated, products, skus);
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

        var placed = await unitOfWork.ExecuteInTransactionAsync(
            token => PlaceOrderCoreAsync(customerId, request, token),
            cancellationToken);

        return placed.IsSuccess && placed.Value is { } order
            ? await StartPaymentAsync(order, cancellationToken)
            : UseCaseResult<PlacedOrderDto>.Failure(placed.Error!.Value, placed.Detail);
    }

    /// <summary>
    /// Asks the gateway for a payment session, after the order is committed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to run inside the transaction, with row locks held on every
    /// product in the basket, on the coupon and on the customer. Those locks
    /// exist to make "the last unit" mean one order — but holding them across a
    /// call to a payment provider means a gateway having a slow thirty seconds
    /// blocks every other shopper trying to buy the same product for thirty
    /// seconds, and a gateway that hangs holds them until the command timeout.
    /// The one thing a checkout must not do is make an outbound network call
    /// the length of a database lock.
    /// </para>
    /// <para>
    /// The order is already saved when this runs, which is the right way round:
    /// a gateway that fails now leaves a real order awaiting payment, which the
    /// shopper can be sent back to. The reverse — a payment session for an order
    /// that was never written — is money taken for nothing.
    /// </para>
    /// </remarks>
    private async Task<UseCaseResult<PlacedOrderDto>> StartPaymentAsync(
        PendingPayment order,
        CancellationToken cancellationToken)
    {
        if (order.Remainder <= 0)
        {
            return new PlacedOrderDto(order.Number, order.PaymentUrl);
        }

        try
        {
            var session = await gateway.StartAsync(order.Number, order.Remainder, cancellationToken);

            // The reference is stored alongside the URL, not just handed to the
            // browser. It is what the shopper comes back with and all a gateway
            // returns — without it on the order there is no way to tell which
            // order an authority settles, and no way for the reconciliation
            // worker to ask about one nobody came back from.
            await settlements.SetPaymentSessionAsync(order.Id, session.PaymentUrl, session.Reference, cancellationToken);

            return new PlacedOrderDto(order.Number, session.PaymentUrl);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Not a failed checkout. The goods are reserved and the order is
            // there to be paid for; what is missing is the redirect, and the
            // shopper is sent back to pay for the order they have rather than
            // to place a second one.
            //
            // Not logged here: this project carries no package references and
            // no logger, deliberately. The endpoint records it — see
            // CheckoutEndpoints.PlaceOrder.
            _ = exception;
            return UseCaseResult<PlacedOrderDto>.Failure(UseCaseError.PaymentUnavailable, order.Number);
        }
    }

    /// <summary>
    /// A committed order and what the gateway still has to collect for it.
    /// </summary>
    /// <remarks>
    /// The transaction returns this rather than the final DTO because the
    /// payment URL is not known until after it commits — see
    /// <see cref="StartPaymentAsync"/>.
    /// </remarks>
    private sealed record PendingPayment(Guid Id, string Number, long Remainder, string? PaymentUrl);

    private async Task<UseCaseResult<PendingPayment>> PlaceOrderCoreAsync(
        Guid customerId,
        PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        // Re-checked inside the transaction: two requests carrying the same key
        // can both pass the check above before either has written anything.
        var duplicate = await repository.FindByIdempotencyKeyAsync(customerId, request.IdempotencyKey, cancellationToken);
        if (duplicate is not null)
        {
            // Nothing left to collect — the first submission already started
            // whatever session there is, and its URL is the one to hand back.
            return new PendingPayment(duplicate.Id, duplicate.Number, 0, duplicate.PaymentUrl);
        }

        var address = await repository.FindAddressAsync(customerId, request.AddressId, cancellationToken);
        if (address is null)
        {
            // Rule 3. Not-found rather than forbidden: an address that exists
            // but belongs to someone else must be indistinguishable from one
            // that does not exist.
            return UseCaseResult<PendingPayment>.Failure(UseCaseError.Invalid, "address");
        }

        var shipping = await repository.FindShippingMethodAsync(request.ShippingMethodId, cancellationToken);
        if (shipping is null)
        {
            return UseCaseResult<PendingPayment>.Failure(UseCaseError.Invalid, "shipping-method");
        }

        var payment = await repository.FindPaymentMethodAsync(request.PaymentMethodId, cancellationToken);
        if (payment is null)
        {
            return UseCaseResult<PendingPayment>.Failure(UseCaseError.Invalid, "payment-method");
        }

        // Locked for update — this is what stops two orders for the last unit
        // from both reading stock 1.
        var products = await repository.LoadProductsForUpdateAsync(
            [.. request.Lines.Select(line => line.ProductId)], cancellationToken);

        var skuIds = request.Lines.Where(line => line.SkuId.HasValue).Select(line => line.SkuId!.Value).ToList();
        IReadOnlyList<ProductSku> skus = skuIds.Count > 0
            ? await repository.LoadSkusForUpdateAsync(skuIds, cancellationToken)
            : [];

        var priced = PriceLines(request.Lines, products, skus);
        if (priced.Error is { } lineError)
        {
            return UseCaseResult<PendingPayment>.Failure(lineError, priced.Detail);
        }

        var discount = Money.Zero;
        Coupon? coupon = null;

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            // Locked, not merely read: a redemption is about to be consumed, and
            // the limit check below is a read-modify-write. See the port.
            coupon = await repository.FindCouponForUpdateAsync(
                request.CouponCode.Trim().ToUpperInvariant(), cancellationToken);
            if (coupon is null)
            {
                return UseCaseResult<PendingPayment>.Failure(UseCaseError.CouponRejected, "unknown");
            }

            if (await repository.CountCustomerRedemptionsAsync(customerId, coupon.Id, cancellationToken) > 0)
            {
                return UseCaseResult<PendingPayment>.Failure(UseCaseError.CouponRejected, "already-used");
            }

            try
            {
                discount = coupon.Validate(priced.Subtotal, clock.UtcNow);
            }
            catch (InvalidOperationException)
            {
                return UseCaseResult<PendingPayment>.Failure(UseCaseError.CouponRejected, "not-applicable");
            }
        }

        // Locked, not merely read — the balance is about to be spent, and a
        // concurrent order must not spend it too. See the port's remarks.
        var customer = await repository.FindCustomerForUpdateAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return UseCaseResult<PendingPayment>.Failure(UseCaseError.Unauthorized);
        }

        // What delivery actually costs on this order.
        //
        // The storefront printed "free over a million" on every product page and
        // nothing applied it, so a shopper who read that, spent one and a half,
        // and reached the payment page was charged anyway — on every order the
        // shop had ever taken.
        //
        // The rule belongs to the chosen method rather than to the shop: a
        // courier that is never free and a post tier that is free over a million
        // are both ordinary, and one shop wants both at once. Keeping a
        // shop-wide figure beside the per-method one would be two places holding
        // the same rule, which is how they come to disagree.
        //
        // Measured against what the customer actually pays for the goods, after
        // any coupon, because that is the number they are looking at when they
        // read the promise.
        var goods = priced.Subtotal.ClampedMinus(discount);

        var shippingCost = shipping.FreeAboveAmount is { } freeAbove && goods.Amount >= freeAbove
            ? Money.Zero
            : shipping.Price;

        var payable = goods + shippingCost;

        // The wallet pays what it can and the gateway collects the rest. It
        // used to be all or nothing: a balance one Toman short of the total
        // bought nothing at all, which is the opposite of what store credit is
        // for. `payment.UsesWallet` now means "draw on the wallet", not "the
        // wallet must cover the whole order".
        var split = WalletSplit.For(payable, customer.WalletBalance, payment.UsesWallet);

        // A method that has no gateway behind it — cash on delivery — cannot
        // collect a remainder, so the wallet has to cover the lot or the order
        // has no way to be paid for.
        if (payment.UsesWallet && !payment.RequiresGateway && !split.FullyCovered)
        {
            return UseCaseResult<PendingPayment>.Failure(UseCaseError.Invalid, "wallet-balance");
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
            payment.Code,
            priced.Subtotal,
            discount,
            shippingCost,
            request.IdempotencyKey,
            coupon?.Code,
            request.Note,
            // No gateway session yet — PaymentUrl is set below, once the
            // gateway has issued one.
            paymentUrl: null,
            deliveryWindow: request.DeliveryWindow,
            walletPaid: split.FromWallet);

        // Rule 2 — reserved, not merely checked. The rows are locked, so this
        // decrement is safe against a concurrent order for the same product.
        foreach (var line in request.Lines)
        {
            if (line.SkuId is { } skuId)
            {
                skus.First(sku => sku.Id == skuId).ReduceStock(line.Quantity);
            }
            else
            {
                products.First(product => product.Id == line.ProductId).ReduceStock(line.Quantity);
            }
        }

        coupon?.RecordRedemption();

        if (split.FromWallet > Money.Zero)
        {
            customer.DebitWallet(split.FromWallet);
            repository.AddWalletTransaction(new WalletTransaction
            {
                CustomerId = customerId,
                Title = $"پرداخت سفارش {number}",
                Amount = -split.FromWallet.Amount,
                Status = WalletTransactionStatus.Success,
                Icon = "shopping_bag",
                CreatedAtUtc = clock.UtcNow,
            });
        }

        repository.AddOrder(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The receipt, after the save — a customer must never be sent one for
        // an order that failed to persist. The mailer swallows its own
        // failures, so a mail server that is down cannot fail a checkout that
        // has already taken money and reserved stock.
        await mailer.SendAsync(
            customer.Email,
            templates.OrderPlaced(
                order.Number,
                order.Id,
                order.PlacedAtUtc,
                order.PaymentMethodName,
                order.ShippingMethodName,
                [.. order.Lines.Select(line => new EmailTemplates.OrderLineView(
                    line.ProductTitle,
                    line.Quantity,
                    (line.UnitPrice * line.Quantity).Amount))],
                order.Discount.Amount,
                order.Shipping.Amount,
                order.Total.Amount),
            cancellationToken);

        // Only the remainder is owed. Handing `payable` to the gateway once the
        // wallet had already been debited would charge the wallet's share twice
        // — the single most expensive thing this method could get wrong.
        //
        // Zero for cash on delivery, so StartPaymentAsync asks for no session:
        // the checkout redirects whenever a URL is present, and one here would
        // send the shopper to pay for money they hand over at the door.
        var owed = payment.RequiresGateway ? split.Remainder.Amount : 0;

        return new PendingPayment(order.Id, order.Number, owed, null);
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
    /// <remarks>
    /// A line naming a <see cref="OrderLineRequest.SkuId"/> is priced and
    /// stock-checked against that <see cref="ProductSku"/> (screen 108) — its
    /// own <c>Price</c>/<c>Stock</c>, not the parent product's, since a
    /// variant's price and stock are exactly what the SKU exists to hold. A
    /// line with no SKU falls back to the product itself, which is still the
    /// whole story for a product with no variants.
    /// </remarks>
    private static PricedBasket PriceLines(
        IReadOnlyList<OrderLineRequest> requested,
        IReadOnlyList<Product> products,
        IReadOnlyList<ProductSku> skus)
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

            if (line.SkuId is { } skuId)
            {
                var sku = skus.FirstOrDefault(candidate => candidate.Id == skuId);

                if (sku is null || sku.ProductId != product.Id || !sku.IsActive)
                {
                    return new PricedBasket([], Money.Zero, UseCaseError.Invalid, "unknown-sku");
                }

                if (sku.Stock < line.Quantity)
                {
                    return new PricedBasket([], Money.Zero, UseCaseError.OutOfStock, product.Slug);
                }

                lines.Add(new OrderLineDraft(
                    product.Id,
                    product.Slug,
                    product.Title,
                    product.ImageUrl,
                    line.Quantity,
                    sku.Price,
                    sku.Id));

                subtotal += sku.Price * line.Quantity;
                continue;
            }

            // A product whose stock is not counted, or that is sold on
            // backorder, has nothing to be short of — those two flags are set
            // per product on the panel's own form, and refusing the order here
            // regardless would make both of them labels. A SKU is always
            // counted: it is a physical variant, and the flags live on the
            // product rather than on each combination of it.
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
