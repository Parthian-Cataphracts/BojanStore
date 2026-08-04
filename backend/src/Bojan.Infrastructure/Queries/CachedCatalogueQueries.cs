using Bojan.Application.Catalogue;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace Bojan.Infrastructure.Queries;

/// <summary>
/// Wraps <see cref="CatalogueQueries"/> with a short in-memory cache for the
/// taxonomy reads — categories, brands, collections — that every storefront
/// page touches for navigation but that an operator changes rarely.
/// </summary>
/// <remarks>
/// Deliberately not applied to <see cref="ListProductsAsync"/> or
/// <see cref="GetProductAsync"/>: price and stock are the two numbers this
/// store cannot afford to show stale, and a five-minute-old "in stock" is
/// exactly the kind of bug a cache silently introduces. Taxonomy has no such
/// risk — a category renamed five minutes ago showing its old name for the
/// rest of that window is a cosmetic lag, not a broken sale.
///
/// A plain TTL (<see cref="CacheDuration"/>) rather than invalidate-on-write:
/// the panel's catalogue writes go through <c>AdminCatalogueService</c>, a
/// different class entirely, and wiring a cache invalidation call into every
/// write path that could touch a category, brand or collection would be a
/// second place for that list to drift out of sync with the first. A cache
/// that is wrong for at most five minutes is a bound every caller can reason
/// about; a cache that is wrong forever because one write path forgot to
/// invalidate it is not.
/// </remarks>
public sealed class CachedCatalogueQueries(CatalogueQueries inner, IMemoryCache cache) : ICatalogueQueries
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private Task<T> Cached<T>(string key, Func<Task<T>> factory) =>
        cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return factory();
        })!;

    public Task<Paged<ProductDto>> ListProductsAsync(ProductQuery query, CancellationToken cancellationToken) =>
        inner.ListProductsAsync(query, cancellationToken);

    public Task<ProductDto?> GetProductAsync(string slug, CancellationToken cancellationToken) =>
        inner.GetProductAsync(slug, cancellationToken);

    public Task<IReadOnlyList<ProductDto>> GetRelatedProductsAsync(string slug, int limit, CancellationToken cancellationToken) =>
        inner.GetRelatedProductsAsync(slug, limit, cancellationToken);

    public Task<IReadOnlyList<ProductDto>> GetProductsBySlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken) =>
        inner.GetProductsBySlugsAsync(slugs, cancellationToken);

    public Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        Cached("catalogue:categories", () => inner.ListCategoriesAsync(cancellationToken));

    public Task<CategoryDto?> GetCategoryAsync(string slug, CancellationToken cancellationToken) =>
        Cached($"catalogue:category:{slug}", () => inner.GetCategoryAsync(slug, cancellationToken));

    public Task<IReadOnlyList<BrandDto>> ListBrandsAsync(CancellationToken cancellationToken) =>
        Cached("catalogue:brands", () => inner.ListBrandsAsync(cancellationToken));

    public Task<BrandDto?> GetBrandAsync(string slug, CancellationToken cancellationToken) =>
        Cached($"catalogue:brand:{slug}", () => inner.GetBrandAsync(slug, cancellationToken));

    public Task<IReadOnlyList<CollectionDto>> ListCollectionsAsync(CancellationToken cancellationToken) =>
        Cached("catalogue:collections", () => inner.ListCollectionsAsync(cancellationToken));

    public Task<CollectionDto?> GetCollectionAsync(string slug, CancellationToken cancellationToken) =>
        Cached($"catalogue:collection:{slug}", () => inner.GetCollectionAsync(slug, cancellationToken));

    public Task<IReadOnlyList<ProductDto>> GetCollectionProductsAsync(string slug, CancellationToken cancellationToken) =>
        inner.GetCollectionProductsAsync(slug, cancellationToken);

    public Task<IReadOnlyList<ArticleDto>> ListArticlesAsync(string? category, CancellationToken cancellationToken) =>
        inner.ListArticlesAsync(category, cancellationToken);

    public Task<ArticleDto?> GetArticleAsync(string slug, CancellationToken cancellationToken) =>
        inner.GetArticleAsync(slug, cancellationToken);

    public Task<IReadOnlyList<ProductReviewDto>> ListProductReviewsAsync(string slug, CancellationToken cancellationToken) =>
        inner.ListProductReviewsAsync(slug, cancellationToken);

    public Task<RatingBreakdownDto> GetRatingBreakdownAsync(string slug, CancellationToken cancellationToken) =>
        inner.GetRatingBreakdownAsync(slug, cancellationToken);

    public Task<IReadOnlyList<ProductQuestionDto>> ListProductQuestionsAsync(string slug, CancellationToken cancellationToken) =>
        inner.ListProductQuestionsAsync(slug, cancellationToken);

    public Task<IReadOnlyList<ProductVariantAxisDto>> ListVariantAxesAsync(string slug, CancellationToken cancellationToken) =>
        inner.ListVariantAxesAsync(slug, cancellationToken);

    public Task<IReadOnlyList<StorefrontSkuDto>> ListSkusAsync(string slug, CancellationToken cancellationToken) =>
        inner.ListSkusAsync(slug, cancellationToken);
}
