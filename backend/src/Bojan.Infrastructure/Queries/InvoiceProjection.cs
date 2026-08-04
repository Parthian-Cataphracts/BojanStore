using Bojan.Application.Contracts;
using Bojan.Domain.Orders;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Queries;

/// <summary>
/// Turns a loaded order into the invoice document both readers get.
/// </summary>
/// <remarks>
/// Shared by <see cref="AccountQueries"/> and <see cref="AdminQueries"/>
/// because the copy the customer holds and the copy the shop keeps have to be
/// the same document. The two callers differ only in how they found the order:
/// the storefront scopes the lookup to one customer, the panel takes any id.
/// </remarks>
internal static class InvoiceProjection
{
    /// <summary>
    /// Builds the invoice for <paramref name="order"/>, or null when it has
    /// none.
    /// </summary>
    /// <param name="order">
    /// Must have been loaded with its <see cref="Order.Lines"/> — the invoice
    /// is priced from them, and an order loaded without them would produce an
    /// empty bill rather than an error.
    /// </param>
    public static async Task<InvoiceDto?> BuildAsync(
        BojanDbContext db,
        Order order,
        CancellationToken cancellationToken)
    {
        if (!InvoiceBuilder.CanInvoice(order))
        {
            return null;
        }

        // Only refunded ones. A return still under review has had no money
        // returned, so the buyer still owes for those goods — see
        // InvoiceBuilder.Build.
        var refunded = await db.ReturnRequests.AsNoTracking()
            .Where(r => r.OrderId == order.Id && r.Status == ReturnStatus.Refunded)
            .Include(r => r.Items)
            .ToListAsync(cancellationToken);

        var invoice = InvoiceBuilder.Build(order, refunded);

        var buyer = await db.Customers.AsNoTracking()
            .Where(c => c.Id == order.CustomerId)
            .Select(c => new { Name = c.FirstName + " " + c.LastName, c.Phone })
            .FirstOrDefaultAsync(cancellationToken);

        return new InvoiceDto(
            order.Id.ToString(),
            invoice.InvoiceNumber,
            invoice.OrderNumber,
            invoice.PlacedAtUtc,
            invoice.IssuedAtUtc,
            (buyer?.Name ?? string.Empty).Trim(),
            buyer?.Phone ?? string.Empty,
            invoice.PaymentMethodName,
            invoice.ShippingMethodName,
            invoice.ShippingAddressSnapshot,
            [.. invoice.Lines.Select(line => new InvoiceLineDto(
                line.ProductId.ToString(),
                line.ProductSlug,
                line.ProductTitle,
                line.Quantity,
                line.UnitPrice.Amount,
                line.LineTotal.Amount))],
            invoice.Subtotal.Amount,
            invoice.CouponCode,
            invoice.Discount.Amount,
            invoice.Shipping.Amount,
            invoice.Total.Amount,
            invoice.ReturnedCount,
            invoice.ReturnedRefund.Amount);
    }
}
