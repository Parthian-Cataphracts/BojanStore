using Bojan.Domain.Common;

namespace Bojan.Domain.Catalogue;

/// <summary>
/// A curated grouping of products — screens 21 and 22.
/// </summary>
/// <remarks>
/// Mirrors the frontend's <c>Collection</c> DTO. That DTO carries
/// <c>productSlugs</c> rather than products, and the storefront resolves them
/// through a second call (<c>GET /collections/{slug}/products</c>), so the
/// membership rows below store an ordering the API projects back into that
/// slug array in the same order an operator arranged it.
/// </remarks>
public sealed class Collection : SoftDeletableEntity
{
    public required string Slug { get; set; }

    public required string Title { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string CoverUrl { get; set; } = string.Empty;

    /// <summary>Pull quote shown in the editorial note on the detail screen.</summary>
    public string? EditorialNote { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsPublished { get; set; } = true;

    private readonly List<CollectionProduct> _products = [];
    public IReadOnlyCollection<CollectionProduct> Products => _products;

    public void AddProduct(Guid productId, int sortOrder) => _products.Add(new CollectionProduct
    {
        CollectionId = Id,
        ProductId = productId,
        SortOrder = sortOrder,
    });

    public void ClearProducts() => _products.Clear();
}

/// <summary>One product's membership of a collection, with the order the operator arranged.</summary>
public sealed class CollectionProduct : Entity
{
    public required Guid CollectionId { get; init; }

    public required Guid ProductId { get; init; }

    public int SortOrder { get; set; }
}
