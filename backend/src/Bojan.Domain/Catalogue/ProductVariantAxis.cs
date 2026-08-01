using Bojan.Domain.Common;

namespace Bojan.Domain.Catalogue;

/// <summary>
/// One selectable axis on the product page — screen 86.
/// </summary>
/// <remarks>
/// Mirrors the frontend's <c>ProductVariantAxis</c>. <see cref="Kind"/> decides
/// how the options draw: <c>swatch</c> renders colour dots (so an option needs
/// a <see cref="ProductVariantOption.Hex"/>), <c>chip</c> renders text pills.
/// </remarks>
public sealed class ProductVariantAxis : Entity
{
    public required Guid ProductId { get; init; }

    /// <summary>Stable key the frontend uses as the axis id, e.g. <c>color</c>.</summary>
    public required string Key { get; set; }

    public required string Label { get; set; }

    public required VariantAxisKind Kind { get; set; }

    public int SortOrder { get; set; }

    private readonly List<ProductVariantOption> _options = [];
    public IReadOnlyCollection<ProductVariantOption> Options => _options;

    public void AddOption(string key, string label, string? hex, bool available, int sortOrder) =>
        _options.Add(new ProductVariantOption
        {
            AxisId = Id,
            Key = key,
            Label = label,
            Hex = hex,
            IsAvailable = available,
            SortOrder = sortOrder,
        });
}

public enum VariantAxisKind
{
    Swatch,
    Chip,
}

public sealed class ProductVariantOption : Entity
{
    public required Guid AxisId { get; init; }

    public required string Key { get; set; }

    public required string Label { get; set; }

    /// <summary>Colour for a <see cref="VariantAxisKind.Swatch"/> axis; null on a chip.</summary>
    public string? Hex { get; set; }

    public bool IsAvailable { get; set; } = true;

    public int SortOrder { get; set; }
}
