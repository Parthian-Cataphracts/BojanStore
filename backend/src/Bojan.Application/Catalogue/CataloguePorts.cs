using Bojan.Application.Common;
using Bojan.Application.Contracts;

namespace Bojan.Application.Catalogue;

/// <summary>
/// The listing's filter, sort and page — the exact parameters
/// <c>apps/storefront/src/lib/api/catalog.ts</c> sends.
/// </summary>
/// <remarks>
/// <see cref="Sort"/>'s five values are the ones in use (<c>newest</c>,
/// <c>bestselling</c>, <c>price-asc</c>, <c>price-desc</c>, <c>rating</c>) and
/// arrive as the hyphenated strings the URL carries, so the type stays
/// <see cref="string"/> rather than an enum the model binder would have to be
/// taught. An unrecognised value falls back to the default ordering rather
/// than erroring — a stale bookmark should still show products.
/// </remarks>
public sealed record ProductQuery(
    string? Category = null,
    string? Brand = null,
    string? Search = null,
    long? MinPrice = null,
    long? MaxPrice = null,
    bool? InStockOnly = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = ProductQuery.DefaultPageSize)
{
    /// <summary>The frontend's own default (<c>catalog.ts</c>); repeated here so a caller that omits it agrees.</summary>
    public const int DefaultPageSize = 24;

    /// <summary>Upper bound on what one request may ask for, so a crafted <c>pageSize</c> cannot pull the whole catalogue.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Page and size clamped into range — a page of 0 or −1 is a bad URL, not an error worth showing.</summary>
    public ProductQuery Normalised() => this with
    {
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize is < 1 or > MaxPageSize ? DefaultPageSize : PageSize,
    };
}

/// <summary>
/// Everything the public catalogue screens read.
/// </summary>
/// <remarks>
/// A query port rather than a repository: these endpoints have no domain
/// behaviour to run, only projections to shape, and pulling entities into
/// memory to map them would cost a join per product on a list of 24. The
/// implementation projects straight to the DTO in SQL.
/// </remarks>
public interface ICatalogueQueries
{
    Task<Paged<ProductDto>> ListProductsAsync(ProductQuery query, CancellationToken cancellationToken);

    Task<ProductDto?> GetProductAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductDto>> GetRelatedProductsAsync(string slug, int limit, CancellationToken cancellationToken);

    /// <summary>Resolves several slugs at once for the comparison screen; unknown slugs are skipped, not errors.</summary>
    Task<IReadOnlyList<ProductDto>> GetProductsBySlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken);

    Task<CategoryDto?> GetCategoryAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<BrandDto>> ListBrandsAsync(CancellationToken cancellationToken);

    Task<BrandDto?> GetBrandAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<CollectionDto>> ListCollectionsAsync(CancellationToken cancellationToken);

    Task<CollectionDto?> GetCollectionAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductDto>> GetCollectionProductsAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArticleDto>> ListArticlesAsync(string? category, CancellationToken cancellationToken);

    Task<ArticleDto?> GetArticleAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Published reviews only — a pending or rejected one must never appear on a product page.</summary>
    Task<IReadOnlyList<ProductReviewDto>> ListProductReviewsAsync(string slug, CancellationToken cancellationToken);

    Task<RatingBreakdownDto> GetRatingBreakdownAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductQuestionDto>> ListProductQuestionsAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductVariantAxisDto>> ListVariantAxesAsync(string slug, CancellationToken cancellationToken);
}
