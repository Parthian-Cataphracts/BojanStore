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

    /// <summary>
    /// Replaces the collection's membership with <paramref name="productIds"/>,
    /// in the order given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is the point: this is a curated grouping, and which product
    /// leads it is an editorial decision the collection screen exists to make.
    /// An empty list is allowed here — a collection an operator has not filled
    /// yet is an ordinary state, unlike a product filed under no category.
    /// </para>
    /// <para>
    /// Only the difference is written, for the same reason as
    /// <see cref="Product.ReplaceCategories"/>: a row per collection per
    /// product is unique, so clearing the list and rebuilding it would delete
    /// each surviving row and insert an identical one — two statements the
    /// database is free to order the wrong way round.
    /// </para>
    /// </remarks>
    public void ReplaceProducts(IEnumerable<Guid> productIds)
    {
        var ordered = productIds.Distinct().ToList();

        _products.RemoveAll(membership => !ordered.Contains(membership.ProductId));

        for (var order = 0; order < ordered.Count; order++)
        {
            var productId = ordered[order];
            var membership = _products.Find(existing => existing.ProductId == productId);

            if (membership is null)
            {
                _products.Add(new CollectionProduct
                {
                    CollectionId = Id,
                    ProductId = productId,
                    SortOrder = order,
                });
            }
            else
            {
                membership.SortOrder = order;
            }
        }
    }
}

/// <summary>One product's membership of a collection, with the order the operator arranged.</summary>
public sealed class CollectionProduct : Entity
{
    public required Guid CollectionId { get; init; }

    public required Guid ProductId { get; init; }

    public int SortOrder { get; set; }
}
