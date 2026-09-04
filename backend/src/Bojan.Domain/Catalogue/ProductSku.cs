using Bojan.Domain.Common;

namespace Bojan.Domain.Catalogue;

/// <summary>
/// One sellable combination of a product — screen 108.
/// </summary>
/// <remarks>
/// <para>
/// A product carries its own <see cref="Product.Sku"/>, <see cref="Product.Price"/>
/// and <see cref="Product.Stock"/>, and for a product with no variants that is
/// the whole story. This exists for the products where it is not: an A5 planner
/// in cream and an A4 in teal are the same product with different codes,
/// barcodes, prices and stock, and none of those fit on the product row.
/// </para>
/// <para>
/// The combination is stored as the axis-option keys it is made of
/// (<see cref="Combination"/>), not as a display string, so renaming an option's
/// label does not orphan the SKU that used it.
/// </para>
/// </remarks>
public sealed class ProductSku : Entity
{
    public required Guid ProductId { get; init; }

    /// <summary>Operator-facing code, unique across the catalogue.</summary>
    public required string Code { get; set; }

    /// <summary>EAN-13 or similar. Optional — not every combination has one printed.</summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// The option keys this combination is, one per axis, in axis order —
    /// e.g. <c>cream|a5</c>. Empty for a product with no axes.
    /// </summary>
    public string Combination { get; set; } = string.Empty;

    /// <summary>
    /// What this combination is sold for.
    /// </summary>
    /// <remarks>
    /// Never the product's price: a shopper who picks size 4 pays what size 4
    /// costs. <c>CheckoutService.PriceLines</c> charges this and nothing else
    /// for a line naming a SKU.
    ///
    /// Zero only alongside a <see cref="CompareAtPrice"/> — that pair is a
    /// hundred-percent discount, and it is the only way a combination can be
    /// free. See <see cref="IsSellable"/>.
    /// </remarks>
    public Money Price { get; set; } = Money.Zero;

    /// <summary>
    /// What this combination cost before its own discount, struck through on
    /// the product page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A discount here is the same shape as the product's own — see
    /// <c>AdminCatalogueService.ApplyDiscountAsync</c>, which discounts by
    /// moving the list price into <c>CompareAtPrice</c> and the reduced one
    /// into <c>Price</c>. Nothing stores a percentage, so nothing can disagree
    /// with what is actually charged.
    /// </para>
    /// <para>
    /// It belongs to this combination alone. Discounting size 2 must not make
    /// size 4 cheaper, and because the pair lives on the row that prices the
    /// line, it cannot: the sizes are separate rows.
    /// </para>
    /// <para>
    /// Null when the combination is not on sale.
    /// </para>
    /// </remarks>
    public Money? CompareAtPrice { get; set; }

    /// <summary>
    /// Whether this combination is priced well enough to be sold.
    /// </summary>
    /// <remarks>
    /// A price of zero with nothing struck through is a combination nobody
    /// priced, and putting it in front of a shopper gives the product away.
    /// Zero *with* a list price above it is a hundred-percent discount, which
    /// is a decision somebody made on purpose.
    /// </remarks>
    public bool IsSellable => Price > Money.Zero || CompareAtPrice is { } listed && listed > Money.Zero;

    public int Stock { get; private set; }

    public bool IsActive { get; set; } = true;

    /// <inheritdoc cref="Product.ReduceStock"/>
    public void ReduceStock(int quantity)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity cannot be negative.");
        }

        if (quantity > Stock)
        {
            throw new InvalidOperationException($"Cannot reduce stock of '{Code}' by {quantity}; only {Stock} available.");
        }

        Stock -= quantity;
    }

    /// <inheritdoc cref="Product.IncreaseStock"/>
    public void IncreaseStock(int quantity)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity cannot be negative.");
        }

        Stock += quantity;
    }

    public void SetStock(int quantity)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Stock cannot be negative.");
        }

        Stock = quantity;
    }
}
