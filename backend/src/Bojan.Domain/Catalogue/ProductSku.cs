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

    public Money Price { get; set; } = Money.Zero;

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

    public void SetStock(int quantity)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Stock cannot be negative.");
        }

        Stock = quantity;
    }
}
