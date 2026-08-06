using Bojan.Domain.Common;

namespace Bojan.Domain.Orders;

/// <summary>The six values of the frontend's <c>ReturnStatus</c> — screens 35 and 36.</summary>
public enum ReturnStatus
{
    Submitted,
    Reviewing,
    Approved,
    Received,
    Refunded,
    Rejected,
}

/// <summary>
/// A request to send something back.
/// </summary>
/// <remarks>
/// <para>
/// The frontend's <c>ReturnRequest</c> DTO is single-product — it carries one
/// <c>productSlug</c>, <c>productTitle</c>, <c>productImage</c> and
/// <c>quantity</c> — while the write it posts
/// (<c>POST /me/returns</c> with <c>orderId, items, reason, description,
/// refundMethod</c>) accepts several items at once. Both are true: a request
/// holds many <see cref="ReturnItem"/> rows, and the read projection surfaces
/// the first one, which is what screens 35 and 36 draw. Fixing that asymmetry
/// is a frontend DTO change, not a backend one, so the model keeps the
/// information rather than throwing it away.
/// </para>
/// <para>
/// The customer files it; an operator decides it. Those are two halves of one
/// object rather than two objects, so the fields the operator writes
/// (<see cref="RefundAmount"/>, <see cref="ReviewNote"/>,
/// <see cref="DecidedById"/>, <see cref="Restocked"/>) live here beside the
/// ones the customer wrote, and none of them can be set except through a
/// transition that reached the state they belong to.
/// </para>
/// </remarks>
public sealed class ReturnRequest : Entity
{
    /// <summary>Human-facing code in the <c>RT-XX-000</c> shape the return screens render.</summary>
    public required string Code { get; init; }

    public required Guid CustomerId { get; init; }

    public required Guid OrderId { get; init; }

    public required string OrderNumber { get; init; }

    public required string Reason { get; set; }

    public string? Description { get; set; }

    /// <summary>Where the money goes back to — wallet or the original card.</summary>
    public string RefundMethod { get; set; } = "wallet";

    /// <summary>
    /// Whether the money goes back to the wallet rather than by hand.
    /// </summary>
    /// <remarks>
    /// The wallet is the only refund this system can actually pay: there is no
    /// adapter behind <c>IPaymentGateway</c> that can reverse a card charge, so
    /// anything else is a figure reported to the operator to settle at the bank.
    /// Matched on the stored code rather than on a display string, for the
    /// reason <see cref="Order.PaymentMethodCode"/> exists.
    /// </remarks>
    public bool RefundsToWallet => string.Equals(RefundMethod, "wallet", StringComparison.Ordinal);

    public ReturnStatus Status { get; private set; } = ReturnStatus.Submitted;

    /// <summary>
    /// What was actually paid back. Zero until the request reaches
    /// <see cref="ReturnStatus.Refunded"/>.
    /// </summary>
    /// <remarks>
    /// Recorded rather than recomputed, for the reason
    /// <see cref="Order.WalletPaid"/> is: the order's prices are frozen but the
    /// rules that turn them into a refund are not, and a figure re-derived a
    /// year later under different rules would contradict the ledger row that was
    /// actually written.
    /// </remarks>
    public Money RefundAmount { get; private set; } = Money.Zero;

    public DateTimeOffset? RefundedAtUtc { get; private set; }

    /// <summary>The operator who last moved it. Null while it is still only the customer's request.</summary>
    public Guid? DecidedById { get; private set; }

    /// <summary>Whatever the operator wrote on the most recent decision.</summary>
    public string? ReviewNote { get; private set; }

    /// <summary>
    /// Whether the goods went back on the shelf when the warehouse took them in.
    /// </summary>
    /// <remarks>
    /// A judgement, not a consequence: the parcel has been out of the shop's
    /// hands and may come back damaged, short, or not the thing that was sold.
    /// <see cref="OrderCancellation.RestocksAutomatically"/> refuses to guess for
    /// exactly this reason once an order has been dispatched, and a return is
    /// that case by definition — so the operator says, and this records what
    /// they said.
    /// </remarks>
    public bool Restocked { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    private readonly List<ReturnItem> _items = [];
    public IReadOnlyCollection<ReturnItem> Items => _items;

    private readonly List<ReturnTimelineEvent> _timeline = [];
    public IReadOnlyCollection<ReturnTimelineEvent> Timeline => _timeline;

    private ReturnRequest()
    {
    }

    public static ReturnRequest Create(
        string code,
        Guid customerId,
        Guid orderId,
        string orderNumber,
        string reason,
        string? description,
        string refundMethod,
        IReadOnlyCollection<ReturnItem> items,
        DateTimeOffset nowUtc)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("A return request must name at least one item.");
        }

        var request = new ReturnRequest
        {
            Code = code,
            CustomerId = customerId,
            OrderId = orderId,
            OrderNumber = orderNumber,
            Reason = reason,
            Description = description,
            RefundMethod = refundMethod,
            CreatedAtUtc = nowUtc,
        };

        request._items.AddRange(items);
        request._timeline.Add(ReturnTimelineEvent.For(request.Id, ReturnStatus.Submitted, nowUtc));
        return request;
    }

    /// <summary>Whether the request has been decided one way or the other.</summary>
    public bool IsClosed => Status is ReturnStatus.Refunded or ReturnStatus.Rejected;

    /// <summary>
    /// Where a status sits on the reviewing path, as a number that can be
    /// compared.
    /// </summary>
    /// <remarks>
    /// <see cref="ReturnStatus.Rejected"/> is not on the path at all — it is a
    /// terminal branch off it, reachable from anywhere, so it has no place in
    /// the ordering and is handled before this is consulted. Same shape, and
    /// same reason, as <c>Order.StageOf</c>.
    /// </remarks>
    private static int StageOf(ReturnStatus status) => status switch
    {
        ReturnStatus.Submitted => 0,
        ReturnStatus.Reviewing => 1,
        ReturnStatus.Approved => 2,
        ReturnStatus.Received => 3,
        ReturnStatus.Refunded => 4,
        _ => int.MaxValue,
    };

    /// <summary>
    /// Moves the request on, appending to the tracker screen 36 draws.
    /// </summary>
    /// <remarks>
    /// Forward-only along the reviewing path, for the reason
    /// <see cref="Order.TransitionTo"/> is: this used to accept any destination
    /// from any open state, so an approved return could be sent back to
    /// "submitted" and the tracker recorded both — a history saying the
    /// warehouse un-received a parcel it already had. Re-sending the state it is
    /// already in is refused too, since it appended a second event and notified
    /// the customer a second time for no change.
    /// </remarks>
    /// <param name="actorId">The operator moving it, named on the entry so the tracker says who.</param>
    /// <returns>The timeline entry this appended — see <c>Order.TransitionTo</c> for why the caller needs it.</returns>
    /// <exception cref="InvalidOperationException">
    /// The request is closed, <paramref name="next"/> is not further along than
    /// where it already is, or it is <see cref="ReturnStatus.Refunded"/> — which
    /// is <see cref="Refund"/>'s to make, because reaching it without naming an
    /// amount would close a request that paid nothing back.
    /// </exception>
    public ReturnTimelineEvent TransitionTo(
        ReturnStatus next,
        DateTimeOffset nowUtc,
        Guid? actorId = null,
        string? note = null)
    {
        if (next is ReturnStatus.Refunded)
        {
            throw new InvalidOperationException(
                $"Return {Code} must be refunded through {nameof(Refund)}, which records what was paid back.");
        }

        return Move(next, nowUtc, actorId, note);
    }

    /// <summary>
    /// Closes the request, recording what went back and when.
    /// </summary>
    /// <remarks>
    /// The amount is the caller's to compute — it comes from the order's frozen
    /// line prices, which this object does not hold — but it cannot be omitted,
    /// and it is written in the same call that moves the status. A refund
    /// recorded in two steps is a refund that can be half-recorded.
    /// </remarks>
    public ReturnTimelineEvent Refund(Money amount, DateTimeOffset nowUtc, Guid? actorId = null, string? note = null)
    {
        var entry = Move(ReturnStatus.Refunded, nowUtc, actorId, note);
        RefundAmount = amount;
        RefundedAtUtc = nowUtc;
        return entry;
    }

    /// <summary>
    /// Records that the returned goods went back into stock.
    /// </summary>
    /// <remarks>
    /// Separate from the transition because it is a separate fact: the warehouse
    /// receiving a parcel and the warehouse judging its contents sellable are
    /// not the same event, and a damaged return is received without being
    /// restocked.
    /// </remarks>
    public void MarkRestocked() => Restocked = true;

    private ReturnTimelineEvent Move(ReturnStatus next, DateTimeOffset nowUtc, Guid? actorId, string? note)
    {
        if (IsClosed)
        {
            throw new InvalidOperationException($"Return {Code} is already closed ({Status}).");
        }

        // Rejected is reachable from anywhere still open — including after the
        // parcel arrived, which is precisely when a return is most often turned
        // down, because that is the first time anyone has seen what came back.
        if (next is not ReturnStatus.Rejected && StageOf(next) <= StageOf(Status))
        {
            throw new InvalidOperationException(
                $"Return {Code} is already at {Status} and cannot move back to {next}.");
        }

        var from = Status;
        Status = next;

        if (actorId is not null)
        {
            DecidedById = actorId;
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            ReviewNote = note.Trim();
        }

        var entry = ReturnTimelineEvent.For(Id, next, nowUtc, from, actorId, note?.Trim());
        _timeline.Add(entry);
        return entry;
    }
}

/// <summary>One product being sent back, captured with the details the return screens render.</summary>
public sealed class ReturnItem : Entity
{
    public required Guid ReturnRequestId { get; init; }

    public required Guid ProductId { get; init; }

    public required string ProductSlug { get; init; }

    public required string ProductTitle { get; init; }

    public required string ProductImageUrl { get; init; }

    public required int Quantity { get; init; }
}

public sealed class ReturnTimelineEvent : Entity
{
    public required Guid ReturnRequestId { get; init; }

    /// <summary>Where the request was before this entry. Null for the entry it is created with.</summary>
    /// <remarks>Mirrors <see cref="OrderTimelineEvent.FromStatus"/>, and for the reason given there.</remarks>
    public ReturnStatus? FromStatus { get; init; }

    public required ReturnStatus Status { get; init; }

    /// <summary>The operator who caused it — null for the entry the customer's own filing creates.</summary>
    public Guid? ActorId { get; init; }

    /// <summary>What the operator wrote when they moved it, where they wrote anything.</summary>
    public string? Reason { get; init; }

    public required DateTimeOffset AtUtc { get; init; }

    public static ReturnTimelineEvent For(
        Guid requestId,
        ReturnStatus status,
        DateTimeOffset nowUtc,
        ReturnStatus? fromStatus = null,
        Guid? actorId = null,
        string? reason = null) => new()
    {
        ReturnRequestId = requestId,
        FromStatus = fromStatus,
        Status = status,
        ActorId = actorId,
        Reason = reason,
        AtUtc = nowUtc,
    };
}
