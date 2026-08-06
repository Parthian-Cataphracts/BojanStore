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

    /// <summary>
    /// The stable code of the method chosen — <c>gateway</c>, <c>wallet</c>,
    /// <c>cod</c> — beside the title shown on screen.
    /// </summary>
    /// <remarks>
    /// The title alone was stored, which is a display string an operator can
    /// rename. Whether an order may ship before it is paid for depends on
    /// whether it is cash on delivery, and that question must not be answerable
    /// only by matching Persian text.
    /// </remarks>
    public required string PaymentMethodCode { get; init; }

    /// <summary>Whether the money has actually been collected — see <see cref="OrderPaymentStatus"/>.</summary>
    public OrderPaymentStatus PaymentStatus { get; private set; } = OrderPaymentStatus.AwaitingPayment;

    public DateTimeOffset? PaidAtUtc { get; private set; }

    /// <summary>The gateway reference or the transfer's tracking number, whichever settled it.</summary>
    public string? PaymentReference { get; private set; }

    /// <summary>The operator who confirmed it. Null when the wallet settled the order on its own.</summary>
    public Guid? SettledById { get; private set; }

    /// <summary>
    /// Cash on delivery is unpaid by definition until it arrives, so it is the
    /// one method allowed to travel the fulfilment path outstanding.
    /// </summary>
    public bool IsCashOnDelivery => string.Equals(PaymentMethodCode, "cod", StringComparison.Ordinal);

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

    /// <summary>
    /// The sixteen-digit invoice number, issued once the order is delivered.
    /// </summary>
    /// <remarks>
    /// Null until then, and that is the whole rule the invoice screens rely on:
    /// an invoice bills what a buyer received, so an order that was never
    /// delivered has nothing to bill and does not appear in the panel's invoice
    /// section. Issued exactly at the <see cref="OrderStatus.Delivered"/>
    /// transition and never re-issued — a number a customer has already been
    /// shown, quoted at support, or filed with an accountant cannot change.
    /// </remarks>
    public string? InvoiceNumber { get; private set; }

    /// <summary>When the order was delivered, and so when its invoice was issued.</summary>
    public DateTimeOffset? DeliveredAtUtc { get; private set; }

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
        string paymentMethodCode,
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
            PaymentMethodCode = paymentMethodCode,
            Subtotal = subtotal,
            Discount = discount,
            Shipping = shipping,
            CouponCode = couponCode,
            Note = note,
            DeliveryWindow = deliveryWindow,
            PaymentUrl = paymentUrl,
            IdempotencyKey = idempotencyKey,
        };

        // An order the wallet covered outright is already paid: the balance was
        // debited in the same transaction that created this, so there is
        // nothing left to collect and nobody to attribute the decision to.
        // Everything else starts outstanding — including cash on delivery,
        // which is outstanding until the courier hands the money over.
        if (fromWallet >= subtotal.ClampedMinus(discount) + shipping)
        {
            // `DateTimeOffset.UtcNow` for the same reason `PlacedAtUtc` uses it:
            // this is the moment the order comes into being, and threading a
            // clock through a constructor that already stamps itself would give
            // one object two ideas of when it was created.
            order.MarkPaid(order.PlacedAtUtc, reference: null, settledBy: null);
        }

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
    /// Records that the money arrived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Idempotent: settling an order that is already paid returns false and
    /// changes nothing, so a double-clicked approve button or a refreshed
    /// callback page credits nothing twice. That guarantee is only worth
    /// anything under the order's row lock, which is where both callers work —
    /// the same arrangement <see cref="WalletTopUp.Approve"/> already relies on.
    /// </para>
    /// <para>
    /// <paramref name="settledBy"/> is null when nothing decided it but the
    /// system: a wallet that covered the whole order has already moved the
    /// money, so there is no person to attribute.
    /// </para>
    /// </remarks>
    /// <returns>False when the order was not awaiting payment.</returns>
    public bool MarkPaid(DateTimeOffset nowUtc, string? reference, Guid? settledBy)
    {
        if (PaymentStatus is not OrderPaymentStatus.AwaitingPayment)
        {
            return false;
        }

        PaymentStatus = OrderPaymentStatus.Paid;
        PaidAtUtc = nowUtc;
        PaymentReference = reference;
        SettledById = settledBy;
        return true;
    }

    /// <summary>Records that a settlement attempt was refused. Idempotent, for the reason <see cref="MarkPaid"/> is.</summary>
    public bool MarkPaymentFailed(string? reference)
    {
        if (PaymentStatus is not OrderPaymentStatus.AwaitingPayment)
        {
            return false;
        }

        PaymentStatus = OrderPaymentStatus.Failed;
        PaymentReference = reference;
        return true;
    }

    /// <summary>
    /// Records that collected money has been given back.
    /// </summary>
    /// <remarks>
    /// Only an order that was actually paid can be refunded. Cancelling one
    /// that never settled leaves it where it was: there is nothing to return,
    /// and marking it Refunded would put a repayment in the books that never
    /// happened.
    /// </remarks>
    public bool MarkRefunded()
    {
        if (PaymentStatus is not OrderPaymentStatus.Paid)
        {
            return false;
        }

        PaymentStatus = OrderPaymentStatus.Refunded;
        return true;
    }

    /// <summary>
    /// The steps an order may not reach until the money is in.
    /// </summary>
    /// <remarks>
    /// Dispatch onwards, not the whole path. Picking and packing against a
    /// transfer that has not cleared is ordinary — card-to-card takes hours —
    /// so the line is drawn where the goods actually leave. Phonix draws it
    /// earlier because it delivers instantly; a warehouse cannot.
    ///
    /// Terminal states are deliberately absent. Cancelling is how an unpaid
    /// order is disposed of.
    /// </remarks>
    private static bool RequiresPaymentBefore(OrderStatus next) =>
        next is OrderStatus.Shipped or OrderStatus.Delivered;

    /// <summary>
    /// Where an order sits on the fulfilment path, as a number that can be
    /// compared.
    /// </summary>
    /// <remarks>
    /// The enum's declaration order already reads Pending, Processing, Packed,
    /// Shipped, Delivered — but Cancelled and Returned sit after Delivered and
    /// are not further along anything, so the ordinal cannot be used directly.
    /// Both are terminal and are refused before this is consulted.
    /// </remarks>
    private static int StageOf(OrderStatus status) => status switch
    {
        OrderStatus.Pending => 0,
        OrderStatus.Processing => 1,
        OrderStatus.Packed => 2,
        OrderStatus.Shipped => 3,
        OrderStatus.Delivered => 4,
        // Terminal, and reached through their own paths rather than by moving
        // along the fulfilment order.
        _ => int.MaxValue,
    };

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
    /// <exception cref="InvalidOperationException">
    /// The order is terminal, or <paramref name="next"/> is not further along
    /// than where it already is.
    /// </exception>
    public OrderTimelineEvent TransitionTo(
        OrderStatus next,
        string? trackingCode = null,
        Guid? actorId = null,
        string? reason = null)
    {
        if (Status is OrderStatus.Cancelled or OrderStatus.Delivered or OrderStatus.Returned)
        {
            throw new InvalidOperationException($"Order {Number} is in a terminal state ({Status}) and cannot transition further.");
        }

        // The forward-only rule this method has always claimed. Only the
        // terminal check existed, so "shipped" could be followed by "pending"
        // and the timeline recorded both — a history that says the parcel went
        // back to being prepared after it left. Re-sending the status it
        // already has is refused for the same reason: it appended a second
        // event and sent the customer a second notification for no change.
        if (StageOf(next) <= StageOf(Status))
        {
            throw new InvalidOperationException(
                $"Order {Number} is already at {Status} and cannot move back to {next}.");
        }

        // Nothing leaves the building on an order nobody has paid for. Two
        // exemptions, both load-bearing:
        //
        // Cash on delivery is the method the rule exists for — outstanding
        // until the courier collects, so refusing to ship it would refuse the
        // method entirely.
        //
        // And the gate covers the fulfilment path only. Cancelling is how an
        // unpaid order is disposed of, so blocking that would trap every order
        // this rule stopped, which is the opposite of the point.
        if (RequiresPaymentBefore(next)
            && PaymentStatus is not OrderPaymentStatus.Paid
            && !IsCashOnDelivery)
        {
            throw new OrderNotPaidException(Number, next);
        }

        var from = Status;
        Status = next;
        if (trackingCode is not null)
        {
            TrackingCode = trackingCode;
        }

        // Issuing the number here rather than in the service that calls this is
        // what makes "delivered" and "has an invoice" the same fact. A second
        // path to delivery added later gets the number without having to
        // remember to ask for it, and the `??=` means no path can re-issue one.
        if (next is OrderStatus.Delivered)
        {
            DeliveredAtUtc ??= DateTimeOffset.UtcNow;
            InvoiceNumber ??= OrderNumber.NewInvoiceNumber();
        }

        var entry = OrderTimelineEvent.For(Id, next, from, actorId, reason);
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

    /// <summary>Where the order was before this entry. Null for the entry the order is created with.</summary>
    /// <remarks>
    /// Adapted from Phonix's <c>OrderStatusHistory</c>, which records the
    /// transition rather than only its destination. A list of destinations
    /// cannot answer "what changed here" once a status can be reached from more
    /// than one place, and it cannot show a move that was reversed at all.
    /// </remarks>
    public OrderStatus? FromStatus { get; init; }

    public required OrderStatus Status { get; init; }

    /// <summary>
    /// Who caused it — an operator, or null when the customer or the system did.
    /// </summary>
    /// <remarks>
    /// The audit trail records that an order changed status and by whom, but it
    /// is a separate table read on a separate screen. Carrying the actor on the
    /// timeline itself means the operator looking at one order can see who
    /// moved it without leaving the page.
    /// </remarks>
    public Guid? ActorId { get; init; }

    /// <summary>Free text the operator gave, where the transition asked for one.</summary>
    public string? Reason { get; init; }

    public required DateTimeOffset AtUtc { get; init; }

    public static OrderTimelineEvent For(
        Guid orderId,
        OrderStatus status,
        OrderStatus? fromStatus = null,
        Guid? actorId = null,
        string? reason = null) => new()
    {
        OrderId = orderId,
        FromStatus = fromStatus,
        Status = status,
        ActorId = actorId,
        Reason = reason,
        AtUtc = DateTimeOffset.UtcNow,
    };
}
