namespace Bojan.Application.Contracts;

/// <summary>One priced row of the invoice document.</summary>
public sealed record InvoiceLineDto(
    string ProductId,
    string ProductSlug,
    string Title,
    int Quantity,
    long UnitPrice,
    long LineTotal);

/// <summary>
/// The customer invoice, as both the storefront (screen 34) and the panel's
/// invoice section render it.
/// </summary>
/// <remarks>
/// <para>
/// One shape for both readers, unlike <see cref="AdminOrderDto"/> and
/// <see cref="OrderDetailDto"/>, which are deliberately separate because the
/// panel sees fields a customer must not. An invoice has no such field: it is
/// the document the buyer is given, so there is nothing on it an operator may
/// see and the buyer may not. Two shapes here would be two chances for the
/// copy the shop keeps and the copy the customer holds to disagree.
/// </para>
/// <para>
/// What differs between the two is the gate in front of it, not the payload —
/// the storefront checks the order belongs to the caller, the panel checks the
/// operator holds the orders section.
/// </para>
/// </remarks>
public sealed record InvoiceDto(
    string OrderId,
    string InvoiceNumber,
    string OrderNumber,
    DateTimeOffset PlacedAt,
    /// <summary>When the order was delivered, which is when the number was issued.</summary>
    DateTimeOffset IssuedAt,
    string CustomerName,
    string CustomerPhone,
    string PaymentMethod,
    string ShippingMethod,
    string Address,
    IReadOnlyList<InvoiceLineDto> Lines,
    long Subtotal,
    string? CouponCode,
    long Discount,
    long Shipping,
    long Total,
    /// <summary>How many units came back on a refunded return, reported rather than itemised.</summary>
    int ReturnedCount,
    /// <summary>What those units were worth, and so roughly what went back to the buyer.</summary>
    long ReturnedRefund,
    /// <summary>
    /// The shop's own words on the document — seller block, closing note, stamp.
    /// </summary>
    /// <remarks>
    /// Carried on the invoice rather than fetched separately by whoever renders
    /// it. The panel could read the settings section directly; the storefront
    /// cannot, since that endpoint is owner-only — and a customer's copy that
    /// silently fell back to defaults while the operator's showed the real
    /// seller details would be two different documents.
    /// </remarks>
    InvoiceSettingsDto Settings);

/// <summary>
/// One row of the panel's invoice list.
/// </summary>
/// <remarks>
/// Lighter than <see cref="InvoiceDto"/> on purpose: the list draws a table of
/// issued invoices and needs no lines, so loading every line of every invoice
/// on the page to render a count would be a join per row for a number the
/// order already knows.
/// </remarks>
public sealed record InvoiceSummaryDto(
    string OrderId,
    string InvoiceNumber,
    string OrderNumber,
    string Customer,
    string CustomerPhone,
    DateTimeOffset IssuedAt,
    int ItemCount,
    long Total);
