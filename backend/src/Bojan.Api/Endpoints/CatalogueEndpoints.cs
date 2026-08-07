using Bojan.Application.Catalogue;
using Bojan.Application.Checkout;
using Microsoft.AspNetCore.Mvc;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Phase 2 — the storefront's public, cacheable reads.
/// </summary>
/// <remarks>
/// <para>
/// Every path here is one the shipped frontend already calls; they were taken
/// from <c>apps/storefront/src/lib/api/catalog.ts</c> and <c>editorial.ts</c>,
/// not proposed. <c>/categories/{slug}</c>, <c>/articles/{slug}</c> and the
/// product's reviews, questions and variants are in that file but not in
/// <c>BACKEND.md</c>'s Phase 2 table — the code is the contract, so they are
/// served too.
/// </para>
/// <para>
/// Cache-Control mirrors the revalidation windows the frontend declares per
/// resource (300s for the catalogue, 3600s for editorial content), so Next's
/// own cache and any proxy in front of it agree about how stale a listing may
/// be.
/// </para>
/// </remarks>
public static class CatalogueEndpoints
{
    private const int CatalogueMaxAge = 300;
    private const int EditorialMaxAge = 3600;

    public static void MapCatalogueEndpoints(this IEndpointRouteBuilder app)
    {
        // Rate limited as a group rather than per route: these were the
        // only public endpoints with no ceiling at all, and adding one to
        // each is a list somebody has to remember to extend. The limit is
        // generous — see RateLimitPolicies.CatalogueRead.
        var group = app.MapGroup(string.Empty)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.CatalogueRead);

        group.MapGet("/products", ListProducts).CacheFor(CatalogueMaxAge);
        // Registered before "/products/{slug}" so the literal wins: a product
        // whose slug happened to be "compare" would otherwise shadow it.
        group.MapGet("/products/compare", CompareProducts).CacheFor(CatalogueMaxAge);
        group.MapGet("/products/{slug}", GetProduct).CacheFor(CatalogueMaxAge);
        group.MapGet("/products/{slug}/related", GetRelated).CacheFor(CatalogueMaxAge);
        group.MapGet("/products/{slug}/reviews", GetReviews).CacheFor(CatalogueMaxAge);
        group.MapGet("/products/{slug}/rating", GetRating).CacheFor(CatalogueMaxAge);
        group.MapGet("/products/{slug}/questions", GetQuestions).CacheFor(CatalogueMaxAge);
        group.MapGet("/products/{slug}/variants", GetVariants).CacheFor(CatalogueMaxAge);
        group.MapGet("/products/{slug}/skus", GetSkus).CacheFor(CatalogueMaxAge);

        group.MapGet("/categories", ListCategories).CacheFor(EditorialMaxAge);
        group.MapGet("/categories/{slug}", GetCategory).CacheFor(EditorialMaxAge);

        group.MapGet("/brands", ListBrands).CacheFor(EditorialMaxAge);
        group.MapGet("/brands/{slug}", GetBrand).CacheFor(EditorialMaxAge);

        group.MapGet("/collections", ListCollections).CacheFor(EditorialMaxAge);
        group.MapGet("/collections/{slug}", GetCollection).CacheFor(EditorialMaxAge);
        group.MapGet("/collections/{slug}/products", GetCollectionProducts).CacheFor(EditorialMaxAge);

        group.MapGet("/articles", ListArticles).CacheFor(EditorialMaxAge);
        group.MapGet("/articles/{slug}", GetArticle).CacheFor(EditorialMaxAge);

        // Checkout needs these before there is a customer, so they sit with the
        // public reads rather than behind the session.
        group.MapGet("/shipping-methods", ListShippingMethods).CacheFor(EditorialMaxAge);
        group.MapGet("/payment-methods", ListPaymentMethods).CacheFor(EditorialMaxAge);
    }

    /// <summary>
    /// The exact query the listing sends — <c>ProductQuery</c> in
    /// <c>apps/storefront/src/lib/api/types.ts</c>. Bound one parameter at a
    /// time rather than as a complex type so a malformed <c>minPrice</c> is a
    /// 400 from the model binder, not a silently ignored filter.
    /// </summary>
    private static async Task<IResult> ListProducts(
        ICatalogueQueries catalogue,
        CancellationToken cancellationToken,
        [FromQuery] string? category = null,
        [FromQuery] string? brand = null,
        [FromQuery] string? search = null,
        [FromQuery] long? minPrice = null,
        [FromQuery] long? maxPrice = null,
        [FromQuery] bool? inStockOnly = null,
        [FromQuery] string? sort = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ProductQuery.DefaultPageSize) =>
        Results.Ok(await catalogue.ListProductsAsync(
            new ProductQuery(category, brand, search, minPrice, maxPrice, inStockOnly, sort, page, pageSize),
            cancellationToken));

    private static async Task<IResult> GetProduct(string slug, ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        await catalogue.GetProductAsync(slug, cancellationToken) is { } product
            ? Results.Ok(product)
            : ApiResults.NotFound();

    private static async Task<IResult> GetRelated(
        string slug,
        ICatalogueQueries catalogue,
        CancellationToken cancellationToken,
        [FromQuery] int limit = 4) =>
        Results.Ok(await catalogue.GetRelatedProductsAsync(slug, limit, cancellationToken));

    /// <summary>
    /// <c>?slugs=a,b,c</c> — the comparison screen keeps its columns in the URL
    /// so a comparison is shareable, and sends them here as one parameter.
    /// </summary>
    private static async Task<IResult> CompareProducts(
        ICatalogueQueries catalogue,
        CancellationToken cancellationToken,
        [FromQuery] string? slugs = null)
    {
        var wanted = (slugs ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return Results.Ok(await catalogue.GetProductsBySlugsAsync(wanted, cancellationToken));
    }

    private static async Task<IResult> GetReviews(string slug, ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        Results.Ok(await catalogue.ListProductReviewsAsync(slug, cancellationToken));

    private static async Task<IResult> GetRating(string slug, ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        Results.Ok(await catalogue.GetRatingBreakdownAsync(slug, cancellationToken));

    private static async Task<IResult> GetQuestions(string slug, ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        Results.Ok(await catalogue.ListProductQuestionsAsync(slug, cancellationToken));

    private static async Task<IResult> GetVariants(string slug, ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        Results.Ok(await catalogue.ListVariantAxesAsync(slug, cancellationToken));

    private static async Task<IResult> GetSkus(string slug, ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        Results.Ok(await catalogue.ListSkusAsync(slug, cancellationToken));

    private static async Task<IResult> ListCategories(ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        Results.Ok(await catalogue.ListCategoriesAsync(cancellationToken));

    private static async Task<IResult> GetCategory(string slug, ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        await catalogue.GetCategoryAsync(slug, cancellationToken) is { } category
            ? Results.Ok(category)
            : ApiResults.NotFound();

    private static async Task<IResult> ListBrands(ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        Results.Ok(await catalogue.ListBrandsAsync(cancellationToken));

    private static async Task<IResult> GetBrand(string slug, ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        await catalogue.GetBrandAsync(slug, cancellationToken) is { } brand
            ? Results.Ok(brand)
            : ApiResults.NotFound();

    private static async Task<IResult> ListCollections(ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        Results.Ok(await catalogue.ListCollectionsAsync(cancellationToken));

    private static async Task<IResult> GetCollection(string slug, ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        await catalogue.GetCollectionAsync(slug, cancellationToken) is { } collection
            ? Results.Ok(collection)
            : ApiResults.NotFound();

    private static async Task<IResult> GetCollectionProducts(
        string slug,
        ICatalogueQueries catalogue,
        CancellationToken cancellationToken) =>
        Results.Ok(await catalogue.GetCollectionProductsAsync(slug, cancellationToken));

    private static async Task<IResult> ListArticles(
        ICatalogueQueries catalogue,
        CancellationToken cancellationToken,
        [FromQuery] string? category = null) =>
        Results.Ok(await catalogue.ListArticlesAsync(category, cancellationToken));

    private static async Task<IResult> GetArticle(string slug, ICatalogueQueries catalogue, CancellationToken cancellationToken) =>
        await catalogue.GetArticleAsync(slug, cancellationToken) is { } article
            ? Results.Ok(article)
            : ApiResults.NotFound();

    private static async Task<IResult> ListShippingMethods(CheckoutService checkout, CancellationToken cancellationToken) =>
        Results.Ok(await checkout.ListShippingMethodsAsync(cancellationToken));

    private static async Task<IResult> ListPaymentMethods(CheckoutService checkout, CancellationToken cancellationToken) =>
        Results.Ok(await checkout.ListPaymentMethodsAsync(cancellationToken));
}

/// <summary>
/// Sets <c>Cache-Control</c> on a public read.
/// </summary>
/// <remarks>
/// Response caching middleware is not registered: the frontend does its own
/// caching against these windows and a second cache in front of it would only
/// make a stale listing harder to explain. The header is set so Next and any
/// CDN can honour the same window the frontend already declares per resource.
/// </remarks>
internal static class CacheHeaderExtensions
{
    public static RouteHandlerBuilder CacheFor(this RouteHandlerBuilder builder, int seconds) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);
            context.HttpContext.Response.Headers.CacheControl = $"public, max-age={seconds}";
            return result;
        });
}
