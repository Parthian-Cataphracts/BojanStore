using Bojan.Application.Contracts;
using Bojan.Domain.Business;
using Bojan.Domain.Orders;

namespace Bojan.Application.Common;

/// <summary>
/// Builds the progress trackers screens 13, 36 and 63 draw.
/// </summary>
/// <remarks>
/// <para>
/// The labels are Persian, and deliberately so. <c>BACKEND.md</c> section 1.4
/// forbids Persian in <em>errors</em>, because the frontend has its own copy
/// for those; a timeline step is not an error, it is content — the DTO
/// declares <c>label: string</c> and the component renders whatever arrives.
/// The strings below are the ones the design draws, so switching the flag
/// changes nothing on screen.
/// </para>
/// <para>
/// A tracker is a fixed list of stages with a cursor, not a log: the design
/// shows all five steps of a fulfilment even when the order has reached the
/// second, with the rest hollow. So the stage list is constant and the order's
/// real timeline rows only decide the cursor and the timestamps.
/// </para>
/// </remarks>
public static class Timelines
{
    private static readonly string[] OrderStages =
        ["ثبت سفارش", "تأیید پرداخت", "آماده‌سازی", "ارسال", "تحویل"];

    /// <summary>How far along the five drawn stages a given status sits.</summary>
    private static int OrderCursor(OrderStatus status) => status switch
    {
        OrderStatus.Pending => 0,
        OrderStatus.Processing => 2,
        OrderStatus.Packed => 2,
        OrderStatus.Shipped => 3,
        OrderStatus.Delivered => 4,
        // A cancelled or returned order never reached the carrier; showing it
        // as "shipped, hollow" would be a lie, so it stays at the first step
        // and the status badge above the tracker carries the real news.
        _ => 0,
    };

    public static IReadOnlyList<OrderTimelineStepDto> ForOrder(
        OrderStatus status,
        IReadOnlyDictionary<OrderStatus, DateTimeOffset> reachedAt)
    {
        var cursor = OrderCursor(status);

        // Stage index -> the status whose timestamp belongs on it. Two stages
        // map to Processing because payment confirmation and preparation are
        // one transition in the model but two rows in the design.
        var stageStatus = new[]
        {
            OrderStatus.Pending,
            OrderStatus.Processing,
            OrderStatus.Processing,
            OrderStatus.Shipped,
            OrderStatus.Delivered,
        };

        return [.. OrderStages.Select((label, index) => new OrderTimelineStepDto(
            $"step-{index + 1}",
            label,
            WireFormat.TimelineState(index, cursor),
            index <= cursor && reachedAt.TryGetValue(stageStatus[index], out var at) ? at : null))];
    }

    private static readonly (string Id, string Label, string Description, string Icon)[] ReturnStages =
    [
        ("submitted", "ثبت درخواست", "با موفقیت ثبت شد", "check"),
        ("reviewing", "بررسی پشتیبانی", "در حال پیگیری توسط کارشناسان", "support_agent"),
        ("approved", "تأیید مرجوعی", "در انتظار تأیید", "fact_check"),
        ("received", "دریافت کالا", "توسط انبار", "inventory_2"),
        ("refunded", "بازگشت وجه / تعویض", "مرحله نهایی", "currency_exchange"),
    ];

    public static IReadOnlyList<ReturnTimelineStepDto> ForReturn(ReturnStatus status)
    {
        // Rejected is a terminal branch off the second stage rather than a
        // sixth stage — the design draws no lane for it.
        var cursor = status switch
        {
            ReturnStatus.Submitted => 0,
            ReturnStatus.Reviewing or ReturnStatus.Rejected => 1,
            ReturnStatus.Approved => 2,
            ReturnStatus.Received => 3,
            ReturnStatus.Refunded => 4,
            _ => 0,
        };

        return [.. ReturnStages.Select((stage, index) => new ReturnTimelineStepDto(
            stage.Id,
            stage.Label,
            stage.Description,
            stage.Icon,
            WireFormat.TimelineState(index, cursor)))];
    }

    private static readonly (string Id, string Label)[] BusinessStages =
    [
        ("submitted", "ثبت درخواست"),
        ("reviewing", "بررسی کارشناس"),
        ("quoted", "صدور پیش‌فاکتور"),
        ("approved", "تأیید نهایی"),
    ];

    public static IReadOnlyList<B2BTimelineStepDto> ForBusinessRequest(
        BusinessRequestStatus status,
        IReadOnlyDictionary<BusinessRequestStatus, DateTimeOffset> reachedAt)
    {
        var cursor = status switch
        {
            BusinessRequestStatus.Submitted => 0,
            BusinessRequestStatus.Reviewing or BusinessRequestStatus.Rejected => 1,
            BusinessRequestStatus.Quoted => 2,
            BusinessRequestStatus.Approved => 3,
            _ => 0,
        };

        var stageStatus = new[]
        {
            BusinessRequestStatus.Submitted,
            BusinessRequestStatus.Reviewing,
            BusinessRequestStatus.Quoted,
            BusinessRequestStatus.Approved,
        };

        return [.. BusinessStages.Select((stage, index) => new B2BTimelineStepDto(
            stage.Id,
            stage.Label,
            index <= cursor && reachedAt.TryGetValue(stageStatus[index], out var at) ? at : null,
            WireFormat.TimelineState(index, cursor)))];
    }
}
