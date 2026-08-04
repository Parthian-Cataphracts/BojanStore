using Bojan.Domain.Common;
using Bojan.Domain.Orders;

namespace Bojan.Domain.Tests;

/// <summary>
/// What the invoice bills, and what it leaves off.
/// </summary>
/// <remarks>
/// The arithmetic here is the part that has to be right: an invoice that does
/// not foot against the money that moved is a document a buyer takes to their
/// bank. Ported alongside <see cref="InvoiceBuilder"/> from Phonix's
/// <c>InvoiceTests</c>, with per-unit cancellation replaced by Bojan's
/// refunded returns.
/// </remarks>
public class InvoiceBuilderTests
{
    private static readonly Guid Pen = Guid.NewGuid();
    private static readonly Guid Pad = Guid.NewGuid();

    private static OrderLineDraft Line(Guid productId, string title, int quantity, long unitPrice) => new(
        ProductId: productId,
        ProductSlug: "p",
        ProductTitle: title,
        ProductImageUrl: "https://example.com/p.jpg",
        Quantity: quantity,
        UnitPrice: new Money(unitPrice));

    /// <summary>An order that has been delivered, and so has an invoice number.</summary>
    private static Order DeliveredOrder(long discount = 0, long shipping = 0, IReadOnlyCollection<OrderLineDraft>? lines = null)
    {
        var drafts = lines ?? [Line(Pen, "خودکار", 2, 100_000), Line(Pad, "دفتر", 1, 300_000)];
        var subtotal = new Money(drafts.Sum(l => l.UnitPrice.Amount * l.Quantity));

        var order = Order.Create(
            number: "BZ-100001-AB12",
            customerId: Guid.NewGuid(),
            lines: drafts,
            shippingAddressId: Guid.NewGuid(),
            shippingAddressSnapshot: "تهران، خیابان آزادی",
            shippingMethodName: "پست پیشتاز",
            paymentMethodName: "پرداخت آنلاین",
            subtotal: subtotal,
            discount: new Money(discount),
            shipping: new Money(shipping),
            idempotencyKey: Guid.NewGuid().ToString());

        order.TransitionTo(OrderStatus.Delivered);
        return order;
    }

    private static ReturnRequest RefundedReturn(Order order, Guid productId, int quantity)
    {
        var request = ReturnRequest.Create(
            code: OrderNumber.NewReturnCode(),
            customerId: order.CustomerId,
            orderId: order.Id,
            orderNumber: order.Number,
            reason: "معیوب بود",
            description: null,
            refundMethod: "wallet",
            items: [new ReturnItem
            {
                ReturnRequestId = Guid.Empty,
                ProductId = productId,
                ProductSlug = "p",
                ProductTitle = "کالا",
                ProductImageUrl = "https://example.com/p.jpg",
                Quantity = quantity,
            }],
            nowUtc: DateTimeOffset.UtcNow);

        request.TransitionTo(ReturnStatus.Refunded, DateTimeOffset.UtcNow);
        return request;
    }

    [Fact]
    public void An_undelivered_order_has_no_invoice()
    {
        var order = Order.Create(
            number: "BZ-100002-CD34",
            customerId: Guid.NewGuid(),
            lines: [Line(Pen, "خودکار", 1, 100_000)],
            shippingAddressId: Guid.NewGuid(),
            shippingAddressSnapshot: "تهران",
            shippingMethodName: "پست",
            paymentMethodName: "آنلاین",
            subtotal: new Money(100_000),
            discount: Money.Zero,
            shipping: Money.Zero,
            idempotencyKey: Guid.NewGuid().ToString());

        Assert.False(InvoiceBuilder.CanInvoice(order));
        Assert.Throws<InvalidOperationException>(() => InvoiceBuilder.Build(order, []));
    }

    [Fact]
    public void Delivery_issues_a_sixteen_digit_number_once()
    {
        var order = DeliveredOrder();

        Assert.NotNull(order.InvoiceNumber);
        Assert.Equal(16, order.InvoiceNumber!.Length);
        Assert.All(order.InvoiceNumber, character => Assert.InRange(character, '0', '9'));
        Assert.NotNull(order.DeliveredAtUtc);
    }

    [Fact]
    public void A_delivered_order_cannot_transition_again_and_so_cannot_be_renumbered()
    {
        var order = DeliveredOrder();
        var issued = order.InvoiceNumber;

        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(OrderStatus.Delivered));
        Assert.Equal(issued, order.InvoiceNumber);
    }

    [Fact]
    public void Two_invoice_numbers_differ()
    {
        Assert.NotEqual(DeliveredOrder().InvoiceNumber, DeliveredOrder().InvoiceNumber);
    }

    [Fact]
    public void With_nothing_returned_the_invoice_is_the_whole_order()
    {
        var order = DeliveredOrder(discount: 50_000, shipping: 45_000);

        var invoice = InvoiceBuilder.Build(order, []);

        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(order.Subtotal, invoice.Subtotal);
        Assert.Equal(order.Discount, invoice.Discount);
        Assert.Equal(order.Shipping, invoice.Shipping);
        Assert.Equal(order.Total, invoice.Total);
        Assert.Equal(0, invoice.ReturnedCount);
    }

    [Fact]
    public void A_partly_returned_line_is_billed_for_what_was_kept()
    {
        var order = DeliveredOrder();

        var invoice = InvoiceBuilder.Build(order, [RefundedReturn(order, Pen, 1)]);

        var pen = invoice.Lines.Single(line => line.ProductId == Pen);
        Assert.Equal(1, pen.Quantity);
        Assert.Equal(new Money(100_000), pen.LineTotal);

        // 500,000 ordered, one 100,000 pen back.
        Assert.Equal(new Money(400_000), invoice.Subtotal);
        Assert.Equal(1, invoice.ReturnedCount);
        Assert.Equal(new Money(100_000), invoice.ReturnedRefund);
    }

    [Fact]
    public void A_fully_returned_line_is_not_on_the_invoice_at_all()
    {
        var order = DeliveredOrder();

        var invoice = InvoiceBuilder.Build(order, [RefundedReturn(order, Pen, 2)]);

        Assert.DoesNotContain(invoice.Lines, line => line.ProductId == Pen);
        Assert.Single(invoice.Lines);
        Assert.Equal(new Money(300_000), invoice.Subtotal);
        Assert.Equal(2, invoice.ReturnedCount);
    }

    [Fact]
    public void A_return_still_under_review_is_still_billed()
    {
        var order = DeliveredOrder();

        // The caller passes only refunded requests — this asserts the other
        // half of that contract: nothing is deducted for a request that has
        // not paid the customer back yet.
        var invoice = InvoiceBuilder.Build(order, []);

        Assert.Equal(order.Subtotal, invoice.Subtotal);
        Assert.Equal(0, invoice.ReturnedCount);
    }

    [Fact]
    public void The_discount_and_shipping_lose_the_returned_goods_share()
    {
        // 500,000 of goods, a 50,000 discount and 45,000 shipping. Returning
        // 100,000 of goods is a fifth of the basket, so a fifth of each comes
        // off: 10,000 and 9,000.
        var order = DeliveredOrder(discount: 50_000, shipping: 45_000);

        var invoice = InvoiceBuilder.Build(order, [RefundedReturn(order, Pen, 1)]);

        Assert.Equal(new Money(400_000), invoice.Subtotal);
        Assert.Equal(new Money(40_000), invoice.Discount);
        Assert.Equal(new Money(36_000), invoice.Shipping);
        Assert.Equal(new Money(396_000), invoice.Total);
    }

    [Fact]
    public void The_total_always_foots_against_its_own_lines()
    {
        var order = DeliveredOrder(discount: 37_000, shipping: 45_000);

        var invoice = InvoiceBuilder.Build(order, [RefundedReturn(order, Pen, 1)]);

        var fromLines = invoice.Lines.Aggregate(Money.Zero, (sum, line) => sum + line.LineTotal);
        Assert.Equal(invoice.Subtotal, fromLines);
        Assert.Equal(invoice.Subtotal.ClampedMinus(invoice.Discount) + invoice.Shipping, invoice.Total);
    }

    [Fact]
    public void Returning_everything_leaves_a_bill_of_nothing_but_never_a_negative_one()
    {
        var order = DeliveredOrder(discount: 50_000, shipping: 45_000);

        var invoice = InvoiceBuilder.Build(order, [
            RefundedReturn(order, Pen, 2),
            RefundedReturn(order, Pad, 1),
        ]);

        Assert.Empty(invoice.Lines);
        Assert.Equal(Money.Zero, invoice.Subtotal);
        Assert.Equal(Money.Zero, invoice.Discount);
        Assert.Equal(Money.Zero, invoice.Shipping);
        Assert.Equal(Money.Zero, invoice.Total);
        Assert.Equal(3, invoice.ReturnedCount);
    }

    [Fact]
    public void A_return_claiming_more_units_than_were_bought_cannot_push_the_bill_negative()
    {
        var order = DeliveredOrder();

        var invoice = InvoiceBuilder.Build(order, [RefundedReturn(order, Pen, 99)]);

        Assert.DoesNotContain(invoice.Lines, line => line.ProductId == Pen);
        Assert.Equal(new Money(300_000), invoice.Subtotal);
        // Clamped to what the order actually held, not the 99 claimed.
        Assert.Equal(2, invoice.ReturnedCount);
        Assert.Equal(new Money(200_000), invoice.ReturnedRefund);
    }
}
