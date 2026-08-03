using Bojan.Domain.Common;

namespace Bojan.Domain.Orders;

/// <summary>
/// A placed order and everything about it that must never change after the
/// fact: the price paid, the items, the addressed destination.
/// </summary>
/// <remarks>
/// <para>
/// The frontend's <c>PlaceOrderInput</c>
/// (<c>apps/storefront/src/lib/api/cart.ts</c>) sends only product ids,
/// quantities, and the chosen address/shipping/payment ids — deliberately no
/// prices, because the basket lives in the shopper's browser and cannot be
/// trusted. <see cref="Create"/> is where the seven rules in
/// <c>BACKEND.md</c> Phase 4 are enforced: everything priced from the
/// database, stock re-checked, the address ownership-checked, both method ids
/// validated, the coupon re-applied, discount clamped, and an empty order
/// rejected.
/// </para>
/// <para>
/// Line prices are captured at order time (<see cref="OrderLine.UnitPrice"/>)
/// rather than joined from the live product — a later price change must never
/// alter a past order's total.
/// </para>
/// </remarks>
public sealed class Order : Entity
{
    /// <summary>Human-facing code in the <c>BJ-######</c> shape the order screens render (see <c>apps/storefront/src/lib/api/cart.ts</c>).</summary>
    public required string Number { get; init; }

    public required Guid CustomerId { get; init; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    public required Guid ShippingAddressId { get; init; }

    /// <summary>Address text captured at order time — an address later edited or deleted must not change history.</summary>
    public required string ShippingAddressSnapshot { get; init; }

    public required string ShippingMethodName { get; init; }

    public required string PaymentMethodName { get; init; }

    public required Money Subtotal { get; init; }

    public required Money Discount { get; init; }

    public required Money Shipping { get; init; }

    public Money Total => Subtotal.ClampedMinus(Discount) + Shipping;

    /// <summary>
    /// How much of <see cref="Total"/> came out of the wallet at placement.
    /// </summary>
    /// <remarks>
    /// Recorded rather than recomputed. The balance moves on after the order —
    /// later top-ups, later orders — so asking the wallet afterwards cannot
    /// answer what this order took from it, and a refund has to put back what
    /// was actually taken. Zero for an order that did not use the wallet.
    /// </remarks>
    public Money WalletPaid { get; init; } = Money.Zero;

    /// <summary>What the gateway is still to collect: the total less the wallet's share.</summary>
    public Money PayableOnline => Total.ClampedMinus(WalletPaid);

    public string? CouponCode { get; init; }

    public string? Note { get; set; }

    /// <summary>
    /// The delivery window the shopper asked for — screen 74's day and slot,
    /// as one already-formatted line ("شنبه ۱۰ مرداد، ۹ تا ۱۲").
    /// </summary>
    /// <remarks>
    /// A preference, not a commitment: nothing schedules against it, and the
    /// courier's actual window is the shipping method's. It is stored because
    /// the screen asks for it and an operator packing the order needs to see
    /// what was asked — before this field the answer was collected and thrown
    /// away. One string rather than a day and a slot column because nothing
    /// queries it; it is read back and shown.
    /// </remarks>
    public string? DeliveryWindow { get; init; }

    public string? TrackingCode { get; set; }

    /// <summary>Gateway redirect URL — present only when payment is not cash on delivery. Mirrors <c>PlacedOrder.paymentUrl</c>.</summary>
    public string? PaymentUrl { get; set; }

    /// <summary>Prevents the same client-submitted request from creating two orders — <c>BACKEND.md</c> Phase 4, rule 7.</summary>
    public required string IdempotencyKey { get; init; }

    public DateTimeOffset PlacedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    private readonly List<OrderLine> _lines = [];
    public IReadOnlyCollection<OrderLine> Lines => _lines;

    private readonly List<OrderTimelineEvent> _timeline = [];
    public IReadOnlyCollection<OrderTimelineEvent> Timeline => _timeline;

    private Order()
    {
    }

    /// <summary>
    /// Builds an order from lines already priced and validated by the
    /// application layer — this constructor trusts its caller completely, so
    /// the checks belong in the use case that calls it, not here.
    /// </summary>
    /// <remarks>
    /// Lines arrive as <see cref="OrderLineDraft"/> rather than as finished
    /// <see cref="OrderLine"/> rows because a line cannot know its
    /// <see cref="OrderLine.OrderId"/> until the order it belongs to exists.
    /// Building them here is what keeps that foreign key from having to be
    /// guessed, back-filled, or left to EF's fix-up on an init-only property.
    /// </remarks>
    public static Order Create(
        string number,
        Guid customerId,
        IReadOnlyCollection<OrderLineDraft> lines,
        Guid shippingAddressId,
        string shippingAddressSnapshot,
        string shippingMethodName,
        string paymentMethodName,
        Money subtotal,
        Money discount,
        Money shipping,
        string idempotencyKey,
        string? couponCode = null,
        string? note = null,
        string? paymentUrl = null,
        string? deliveryWindow = null,
        Money? walletPaid = null)
    {
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("An order must have at least one line.");
        }

        if (discount > subtotal)
        {
            throw new InvalidOperationException("Discount cannot exceed the order subtotal.");
        }

        var fromWallet = walletPaid ?? Money.Zero;
        if (fromWallet > subtotal.ClampedMinus(discount) + shipping)
        {
            throw new InvalidOperationException("The wallet cannot pay more than the order is worth.");
        }

        var order = new Order
        {
            WalletPaid = fromWallet,
            Number = number,
            CustomerId = customerId,
            ShippingAddressId = shippingAddressId,
            ShippingAddressSnapshot = shippingAddressSnapshot,
            ShippingMethodName = shippingMethodName,
            PaymentMethodName = paymentMethodName,
            Subtotal = subtotal,
            Discount = discount,
            Shipping = shipping,
            CouponCode = couponCode,
            Note = note,
            DeliveryWindow = deliveryWindow,
            PaymentUrl = paymentUrl,
            IdempotencyKey = idempotencyKey,
        };

        order._lines.AddRange(lines.Select(line => new OrderLine
        {
            OrderId = order.Id,
            ProductId = line.ProductId,
            SkuId = line.SkuId,
            ProductSlug = line.ProductSlug,
            ProductTitle = line.ProductTitle,
            ProductImageUrl = line.ProductImageUrl,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
        }));

        order._timeline.Add(OrderTimelineEvent.For(order.Id, OrderStatus.Pending));
        return order;
    }

    /// <summary>
    /// Transitions the order, appending to its timeline. Backward transitions
    /// (delivered -> pending) are rejected — screen 95's status control must
    /// only move forward or to a terminal state.
    /// </summary>
    /// <returns>
    /// The timeline entry this appended. The caller returns it to its
    /// repository so the new row is tracked as an insert: EF infers the state
    /// of an entity found in a tracked parent's collection from whether its key
    /// is already set, and <see cref="Entity.Id"/> assigns one at construction,
    /// which reads as "already exists".
    /// </returns>
    public OrderTimelineEvent TransitionTo(OrderStatus next, string? trackingCode = null)
    {
        if (Status is OrderStatus.Cancelled or OrderStatus.Delivered or OrderStatus.Returned)
        {
            throw new InvalidOperationException($"Order {Number} is in a terminal state ({Status}) and cannot transition further.");
        }

        Status = next;
        if (trackingCode is not null)
        {
            TrackingCode = trackingCode;
        }

        var entry = OrderTimelineEvent.For(Id, next);
        _timeline.Add(entry);
        return entry;
    }
}

/// <summary>
/// A line the application layer has priced, before the order that will own it
/// exists. Carries everything <see cref="OrderLine"/> does except the foreign
/// key <see cref="Order.Create"/> supplies.
/// </summary>
public sealed record OrderLineDraft(
    Guid ProductId,
    string ProductSlug,
    string ProductTitle,
    string ProductImageUrl,
    int Quantity,
    Money UnitPrice,
    Guid? SkuId = null);

/// <summary>One product line within an order, priced at the moment the order was placed.</summary>
public sealed class OrderLine : Entity
{
    public required Guid OrderId { get; init; }

    public required Guid ProductId { get; init; }

    /// <summary>The variant sold, when the product has any (screen 108) — null for a product with none.</summary>
    public Guid? SkuId { get; init; }

    /// <summary>Captured at order time — the product's slug, title and image may change later.</summary>
    public required string ProductSlug { get; init; }

    public required string ProductTitle { get; init; }

    public required string ProductImageUrl { get; init; }

    public required int Quantity { get; init; }

    /// <summary>Price per unit, from the database at order time — never the price the client submitted.</summary>
    public required Money UnitPrice { get; init; }
}

/// <summary>One entry in the fulfilment timeline drawn on screen 13.</summary>
public sealed class OrderTimelineEvent : Entity
{
    public required Guid OrderId { get; init; }

    public required OrderStatus Status { get; init; }

    public required DateTimeOffset AtUtc { get; init; }

    public static OrderTimelineEvent For(Guid orderId, OrderStatus status) => new()
    {
        OrderId = orderId,
        Status = status,
        AtUtc = DateTimeOffset.UtcNow,
    };
}
