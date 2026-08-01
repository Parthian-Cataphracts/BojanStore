using Bojan.Domain.Common;

namespace Bojan.Domain.Catalogue;

/// <summary>
/// A named property of a product, with the values it may take — screen 106.
/// </summary>
/// <remarks>
/// <para>
/// Distinct from <see cref="ProductSpec"/>, which is one label and one value
/// for the specification table on the product page. An attribute declares the
/// set a value is drawn from ("گرماژ: ۷۰، ۸۰، ۹۰") and whether the catalogue may
/// filter on it, which a free-text spec row cannot express.
/// </para>
/// <para>
/// Also distinct from <see cref="ProductVariantAxis"/>, which is narrower: an
/// axis produces sellable combinations and therefore SKUs. Paper weight is
/// something a shopper filters by; it is not necessarily something you buy a
/// separate unit of.
/// </para>
/// </remarks>
public sealed class ProductAttribute : Entity
{
    public required Guid ProductId { get; init; }

    public required string Name { get; set; }

    public required AttributeKind Kind { get; set; }

    /// <summary>
    /// The values this attribute may take, in the order they were entered.
    /// Stored as one delimited string rather than a child table: they are only
    /// ever read and written together, and never joined against.
    /// </summary>
    public string Values { get; set; } = string.Empty;

    /// <summary>Whether the catalogue offers this as a filter.</summary>
    public bool IsFilterable { get; set; }

    public int SortOrder { get; set; }
}

public enum AttributeKind
{
    Text,
    Number,
    Boolean,
}
