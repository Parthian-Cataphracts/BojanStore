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
/// The frontend's <c>ReturnRequest</c> DTO is single-product — it carries one
/// <c>productSlug</c>, <c>productTitle</c>, <c>productImage</c> and
/// <c>quantity</c> — while the write it posts
/// (<c>POST /me/returns</c> with <c>orderId, items, reason, description,
/// refundMethod</c>) accepts several items at once. Both are true: a request
/// holds many <see cref="ReturnItem"/> rows, and the read projection surfaces
/// the first one, which is what screens 35 and 36 draw. Fixing that asymmetry
/// is a frontend DTO change, not a backend one, so the model keeps the
/// information rather than throwing it away.
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

    public ReturnStatus Status { get; private set; } = ReturnStatus.Submitted;

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

    /// <summary>Moves the request on, appending to the tracker screen 36 draws.</summary>
    /// <returns>The timeline entry this appended — see <c>Order.TransitionTo</c> for why the caller needs it.</returns>
    public ReturnTimelineEvent TransitionTo(ReturnStatus next, DateTimeOffset nowUtc)
    {
        if (Status is ReturnStatus.Refunded or ReturnStatus.Rejected)
        {
            throw new InvalidOperationException($"Return {Code} is already closed ({Status}).");
        }

        Status = next;
        var entry = ReturnTimelineEvent.For(Id, next, nowUtc);
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

    public required ReturnStatus Status { get; init; }

    public required DateTimeOffset AtUtc { get; init; }

    public static ReturnTimelineEvent For(Guid requestId, ReturnStatus status, DateTimeOffset nowUtc) => new()
    {
        ReturnRequestId = requestId,
        Status = status,
        AtUtc = nowUtc,
    };
}
