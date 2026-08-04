using Bojan.Domain.Common;

namespace Bojan.Domain.Orders;

/// <summary>One line of an invoice: something the buyer actually kept.</summary>
public sealed record InvoiceLine(
    Guid ProductId,
    string ProductSlug,
    string ProductTitle,
    int Quantity,
    Money UnitPrice,
    Money LineTotal);

/// <summary>
/// A customer invoice, priced from the order's own figures.
/// </summary>
/// <remarks>
/// Every amount here is derived rather than stored, for the reason
/// <see cref="Order.Total"/> is: a stored total can disagree with the lines it
/// came from, and an invoice that disagrees with itself is worse than no
/// invoice.
/// </remarks>
public sealed record Invoice(
    string InvoiceNumber,
    string OrderNumber,
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset IssuedAtUtc,
    string PaymentMethodName,
    string ShippingMethodName,
    string ShippingAddressSnapshot,
    IReadOnlyList<InvoiceLine> Lines,
    Money Subtotal,
    string? CouponCode,
    Money Discount,
    Money Shipping,
    Money Total,
    int ReturnedCount,
    Money ReturnedRefund);

/// <summary>
/// Builds the customer invoice for a delivered order.
/// </summary>
/// <remarks>
/// <para>
/// An invoice bills what the buyer <em>kept</em>, not what they ordered. When
/// a return is refunded those units went back and so did the money, and
/// leaving them on the invoice would bill someone for goods they no longer
/// have and were paid back for. They come off the lines entirely and are
/// reported once, as a count and a sum — a returned product has no place on
/// the buyer's bill, and itemising it invites the reader to add it up.
/// </para>
/// <para>
/// Ported from Phonix's <c>InvoiceBuilder</c>, which excluded per-unit
/// cancellations from an order. Bojan has no per-unit fulfilment: whole orders
/// are cancelled (and a cancelled order is never delivered, so it never has an
/// invoice at all), and individual products come back through
/// <see cref="ReturnRequest"/>. Refunded return items are therefore the exact
/// analogue, and the arithmetic below is Phonix's unchanged.
/// </para>
/// <para>
/// Kept in the domain and free of any storage concern for the same reason
/// <see cref="OrderCancellation"/> is: the figure printed on a customer's
/// invoice and the figure an operator sees in the panel are read from two
/// different places, and they have to be computed by one piece of code.
/// </para>
/// </remarks>
public static class InvoiceBuilder
{
    /// <summary>
    /// Whether an order can be invoiced at all.
    /// </summary>
    /// <remarks>
    /// Both halves are the same fact — <see cref="Order.TransitionTo"/> issues
    /// the number exactly at delivery — but an order delivered before this
    /// feature existed has the status and no number, and cannot be billed
    /// because there is nothing to print at the top of the page.
    /// </remarks>
    public static bool CanInvoice(Order order) =>
        order.Status is OrderStatus.Delivered && !string.IsNullOrWhiteSpace(order.InvoiceNumber);

    /// <summary>
    /// Builds the invoice for <paramref name="order"/>, less anything
    /// <paramref name="refundedReturns"/> says came back.
    /// </summary>
    /// <param name="refundedReturns">
    /// Return requests against this order that reached
    /// <see cref="ReturnStatus.Refunded"/>. Requests still under review are not
    /// here on purpose: nothing has been paid back yet, so the buyer still owes
    /// for those goods and the invoice still shows them. A request refunded
    /// later re-renders the invoice with them removed, which is correct — the
    /// invoice states what is owed now, and it is regenerated on every read
    /// rather than frozen at delivery.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The order has not been delivered. Callers should test
    /// <see cref="CanInvoice"/> and answer the request rather than let this
    /// surface as a fault.
    /// </exception>
    public static Invoice Build(Order order, IReadOnlyCollection<ReturnRequest> refundedReturns)
    {
        if (!CanInvoice(order))
        {
            throw new InvalidOperationException($"Order {order.Number} has no invoice: it has not been delivered.");
        }

        // How many units of each product came back. Keyed by product rather
        // than by order line because a return names a product, not a line —
        // and an order cannot hold the same product on two lines (the basket
        // merges them by product before checkout).
        var returned = refundedReturns
            .SelectMany(request => request.Items)
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var lines = new List<InvoiceLine>();
        var returnedCount = 0;
        var returnedValue = Money.Zero;

        foreach (var line in order.Lines.OrderBy(l => l.ProductTitle, StringComparer.Ordinal))
        {
            // Clamped: a return recording more units than the order held is
            // bad data, and it must reduce the bill to zero rather than turn
            // it negative.
            var back = Math.Clamp(returned.GetValueOrDefault(line.ProductId), 0, line.Quantity);
            var billed = line.Quantity - back;

            returnedCount += back;
            returnedValue += line.UnitPrice * back;

            if (billed <= 0)
            {
                continue; // every unit came back — the line is not on the bill
            }

            lines.Add(new InvoiceLine(
                line.ProductId,
                line.ProductSlug,
                line.ProductTitle,
                billed,
                line.UnitPrice,
                line.UnitPrice * billed));
        }

        var subtotal = order.Subtotal.ClampedMinus(returnedValue);

        // The returned goods' share of the order's own discount and shipping.
        // Subtracting a slice of the real figures, rather than re-running the
        // coupon against the smaller basket, is what keeps the invoice footing
        // against the money that actually moved: a coupon with a minimum spend
        // or a tiered shipping rate would price the reduced basket differently
        // from how it was charged, and the buyer was charged the original way.
        var discount = order.Discount.ClampedMinus(Share(order.Discount, returnedValue, order.Subtotal));
        var shipping = order.Shipping.ClampedMinus(Share(order.Shipping, returnedValue, order.Subtotal));

        return new Invoice(
            order.InvoiceNumber!,
            order.Number,
            order.PlacedAtUtc,
            order.DeliveredAtUtc ?? order.PlacedAtUtc,
            order.PaymentMethodName,
            order.ShippingMethodName,
            order.ShippingAddressSnapshot,
            lines,
            subtotal,
            order.CouponCode,
            discount,
            shipping,
            subtotal.ClampedMinus(discount) + shipping,
            returnedCount,
            returnedValue);
    }

    /// <summary>
    /// The part of <paramref name="total"/> that belongs to
    /// <paramref name="part"/> of <paramref name="whole"/>.
    /// </summary>
    /// <remarks>
    /// Rounded away from zero so the shop never withholds a fraction more of a
    /// discount than the returned goods earned, and clamped so a part larger
    /// than the whole — which only bad data produces — cannot subtract more
    /// than there is.
    /// </remarks>
    private static Money Share(Money total, Money part, Money whole)
    {
        if (total == Money.Zero || part == Money.Zero || whole == Money.Zero)
        {
            return Money.Zero;
        }

        var amount = (long)Math.Round(
            total.Amount * (decimal)part.Amount / whole.Amount,
            MidpointRounding.AwayFromZero);

        return new Money(Math.Clamp(amount, 0, total.Amount));
    }
}
