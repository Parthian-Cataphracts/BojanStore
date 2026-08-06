using Bojan.Domain.Orders;

namespace Bojan.Application.Orders;

/// <summary>
/// The Persian copy a customer sees when something happens to their order.
/// </summary>
/// <remarks>
/// <para>
/// Adapted from Phonix's <c>OrderNotices</c>, and for the reason that file
/// gives: keeping the wording in one place means it can be reviewed as writing
/// rather than found a sentence at a time inside transaction logic. It was
/// already drifting here — the status line lived as a private switch on
/// <c>AdminOperationsService</c>, the cancellation wording as string
/// interpolation inside <c>OrderCancellationService</c>, and neither knew the
/// other existed.
/// </para>
/// <para>
/// Copy only. Nothing here reads or writes anything, so the services that call
/// it stay the only places that move money or state, and this stays the only
/// place that decides how it is worded.
/// </para>
/// </remarks>
public static class OrderNotices
{
    /// <summary>What a fulfilment step tells the customer.</summary>
    public static string StatusChanged(OrderStatus status, string number) => status switch
    {
        OrderStatus.Processing => $"سفارش {number} در حال آماده‌سازی است.",
        OrderStatus.Packed => $"سفارش {number} بسته‌بندی شد.",
        OrderStatus.Shipped => $"سفارش {number} ارسال شد.",
        OrderStatus.Delivered => $"سفارش {number} تحویل داده شد.",
        OrderStatus.Cancelled => $"سفارش {number} لغو شد.",
        OrderStatus.Returned => $"سفارش {number} مرجوع شد.",
        _ => $"وضعیت سفارش {number} به‌روزرسانی شد.",
    };

    /// <summary>An operator has confirmed the money arrived.</summary>
    public static string PaymentConfirmed(string number) =>
        $"پرداخت سفارش {number} تأیید شد و سفارش وارد مرحله آماده‌سازی می‌شود.";

    /// <summary>
    /// A cancellation, with whatever went back to the wallet.
    /// </summary>
    /// <remarks>
    /// The penalty is named separately rather than folded into the refund. A
    /// customer who is told only the smaller number has to work out the
    /// difference themselves, which is how a support ticket starts.
    /// </remarks>
    public static string Cancelled(string number, long refund, long penalty)
    {
        if (refund == 0)
        {
            return $"سفارش {number} لغو شد.";
        }

        var refunded = $"سفارش {number} لغو شد و {refund:N0} تومان به کیف پول شما بازگشت.";

        return penalty > 0
            ? $"{refunded} مبلغ {penalty:N0} تومان بابت جریمه لغو کسر شد."
            : refunded;
    }
}
