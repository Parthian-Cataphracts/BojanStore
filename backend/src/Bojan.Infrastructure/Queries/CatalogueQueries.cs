using Bojan.Application.Catalogue;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
using Bojan.Domain.Content;
using Bojan.Domain.Reviews;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Queries;

/// <summary>
/// Phase 2's reads, projected in SQL.
/// </summary>
/// <remarks>
/// <para>
/// Filtering and ordering happen on the <see cref="Product"/> query, before the
/// join and projection, so the database does the work: a price filter written
/// against the projected row's <c>long</c> would instead be applied after every
/// candidate row had already been fetched and joined.
/// </para>
/// <para>
/// <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{T}"/> everywhere:
/// nothing here is written back, and the change tracker on a page of 24
/// products is pure cost.
/// </para>
/// </remarks>
public sealed class CatalogueQueries(BojanDbContext db) : ICatalogueQueries
{
    /// <summary>The flat shape a product card needs — no gallery, no specs, and never the cost price.</summary>
    private sealed record ProductRow(
        Guid Id,
        string Slug,
        string Title,
        string Brand,
        string BrandSlug,
        string CategorySlug,
        string CategoryName,
        Money Price,
        Money? CompareAtPrice,
        double Rating,
        int ReviewCount,
        int Stock,
        string Image,
        string ImageAlt,
        string? Description,
        bool IsNew,
        bool IsBestseller);

    private IQueryable<Product> PublishedProducts() =>
        db.Products.AsNoTracking().Where(p => p.IsPublished);

    /// <summary>
    /// Joins a filtered product query to its brand and category and projects
    /// the card row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two joins are outer, and that is load-bearing. <see cref="Brand"/>
    /// and <see cref="Category"/> both carry a soft-delete query filter, which
    /// an inner join folds into its own condition — so archiving a brand did
    /// not hide the brand, it removed every one of its products from the
    /// storefront. They stayed published, the panel went on listing them as
    /// published, and they vanished from the listing, from search, from their
    /// category page and from the sitemap, with nothing in the log. An archived
    /// brand is a brand the shop no longer promotes, not a shelf it silently
    /// empties.
    /// </para>
    /// <para>
    /// Rating and review count are computed from the published reviews rather
    /// than denormalised onto the product. That costs a correlated subquery per
    /// row, and buys a rating that cannot drift from the reviews under it — the
    /// failure mode of a cached average is a product page whose stars disagree
    /// with the reviews printed below them.
    /// </para>
    /// </remarks>
    private IQueryable<ProductRow> Project(IQueryable<Product> products) =>
        from product in products
        join brandMatch in db.Brands.AsNoTracking() on product.BrandId equals brandMatch.Id into brandMatches
        from brand in brandMatches.DefaultIfEmpty()
        join categoryMatch in db.Categories.AsNoTracking() on product.CategoryId equals categoryMatch.Id into categoryMatches
        from category in categoryMatches.DefaultIfEmpty()
        select new ProductRow(
            product.Id,
            product.Slug,
            product.Title,
            // Null where the brand or category has been archived. The DTO's
            // fields are non-nullable strings the cards render directly, so an
            // empty one is the honest answer rather than a crash.
            brand.Name ?? string.Empty,
            brand.Slug ?? string.Empty,
            category.Slug ?? string.Empty,
            category.Name ?? string.Empty,
            product.Price,
            product.CompareAtPrice,
            db.ProductReviews
                .Where(r => r.ProductId == product.Id && r.Status == ModerationStatus.Published)
                .Select(r => (double?)r.Rating)
                .Average() ?? 0d,
            db.ProductReviews.Count(r => r.ProductId == product.Id && r.Status == ModerationStatus.Published),
            product.Stock,
            product.ImageUrl,
            product.ImageAlt,
            product.Description,
            product.IsNew,
            product.IsBestseller);

    private static ProductDto ToDto(
        ProductRow row,
        IReadOnlyList<string>? gallery = null,
        IReadOnlyList<ProductSpecDto>? specs = null) =>
        new(row.Id.ToString(),
            row.Slug,
            row.Title,
            row.Brand,
            row.BrandSlug,
            row.CategorySlug,
            row.CategoryName,
            row.Price.Amount,
            row.CompareAtPrice?.Amount,
            Math.Round(row.Rating, 1),
            row.ReviewCount,
            row.Stock,
            row.Image,
            row.ImageAlt,
            gallery,
            row.Description,
            specs,
            row.IsNew,
            row.IsBestseller);

    public async Task<Paged<ProductDto>> ListProductsAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var products = PublishedProducts();

        if (!string.IsNullOrWhiteSpace(normalised.Category))
        {
            // A parent category shows everything beneath it: picking
            // "نوشت‌افزار" and seeing none of its children's products is the
            // most common complaint a flat filter produces.
            //
            // One query, not two. This used to read the matching category ids
            // and then read their children, both round trips before the
            // products query had started — three requests to draw one filtered
            // page. The set is small and every lookup is on an indexed slug, so
            // folding it into a single statement costs the planner nothing and
            // saves two waits on the wire.
            var categoryIds = await db.Categories.AsNoTracking()
                .Where(c => c.Slug == normalised.Category
                    || (c.ParentId != null && db.Categories
                        .Any(parent => parent.Id == c.ParentId && parent.Slug == normalised.Category)))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            // A slug nothing matches is an empty page, not an error — a stale
            // bookmark or a renamed category should not read as a fault.
            if (categoryIds.Count == 0)
            {
                return new Paged<ProductDto>([], 0, normalised.Page, normalised.PageSize);
            }

            // Against the join rather than the product's own primary column: a
            // product may be filed under several categories, and browsing one
            // of the others has to find it. Every product has a join row for
            // its primary too, so this is a superset of what the column
            // matched, never a smaller one.
            products = products.Where(p => db.ProductCategories
                .Any(filing => filing.ProductId == p.Id && categoryIds.Contains(filing.CategoryId)));
        }

        if (!string.IsNullOrWhiteSpace(normalised.Brand))
        {
            // Correlated rather than resolved first, for the same reason: the
            // brand slug is unique and indexed, so this is one keyed lookup
            // inside a plan the database was going to build anyway.
            products = products.Where(p => db.Brands
                .Any(b => b.Id == p.BrandId && b.Slug == normalised.Brand));
        }

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            /*
              Folded on both sides, so «ابرنگ» finds «آبرنگ».

              Persian is written several ways for one word — the madda on the
              alef, Arabic ك and ي against Persian ک and ی, a compound joined
              with a half-space or a full one or neither — and a shopper who
              types any of them is looking for the same thing. Comparing what
              was typed against what was stored answers «چیزی پیدا نشد» for a
              shop that stocks it. See PersianText.Fold, and bojan_fold, which
              is the same fold in SQL so the column is folded where it lives
              rather than by reading every row into memory.
            */
            var needle = PersianText.Fold(normalised.Search);
            products = products.Where(p =>
                BojanDbContext.Fold(p.Title).Contains(needle)
                || BojanDbContext.Fold(p.Sku).Contains(needle)
                || db.Brands.Any(b => b.Id == p.BrandId && BojanDbContext.Fold(b.Name).Contains(needle))
                || db.ProductCategories.Any(filing => filing.ProductId == p.Id && db.Categories
                    .Any(c => c.Id == filing.CategoryId && BojanDbContext.Fold(c.Name).Contains(needle))));
        }

        if (normalised.InStockOnly == true)
        {
            products = products.Where(p => p.Stock > 0);
        }

        if (normalised.MinPrice is { } min)
        {
            products = products.Where(p => p.Price.Amount >= min);
        }

        if (normalised.MaxPrice is { } max)
        {
            products = products.Where(p => p.Price.Amount <= max);
        }

        // Counted before ordering and paging — the total is the size of the
        // filtered set, not of the page.
        var total = await products.CountAsync(cancellationToken);

        // An unrecognised sort falls through to the default rather than
        // erroring: a stale bookmark should still show products.
        products = normalised.Sort switch
        {
            "price-asc" => products.OrderBy(p => p.Price.Amount),
            "price-desc" => products.OrderByDescending(p => p.Price.Amount),
            "rating" => products.OrderByDescending(p => db.ProductReviews
                .Where(r => r.ProductId == p.Id && r.Status == ModerationStatus.Published)
                .Select(r => (double?)r.Rating).Average() ?? 0d),
            "bestselling" => products.OrderByDescending(p => p.IsBestseller).ThenBy(p => p.Slug),
            "newest" => products.OrderByDescending(p => p.IsNew).ThenBy(p => p.Slug),
            _ => products.OrderBy(p => p.Slug),
        };

        var page = await Project(products
                .Skip((normalised.Page - 1) * normalised.PageSize)
                .Take(normalised.PageSize))
            .ToListAsync(cancellationToken);

        return new Paged<ProductDto>([.. page.Select(row => ToDto(row))], total, normalised.Page, normalised.PageSize);
    }

    public async Task<ProductDto?> GetProductAsync(string slug, CancellationToken cancellationToken)
    {
        var row = await Project(PublishedProducts().Where(p => p.Slug == slug))
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        /*
          The detail screen is the one place gallery and specs are shown, so
          they are read here and nowhere else — but in one statement rather
          than the two they used to take. Neither depends on the other and both
          are keyed by the id already in hand, so the second wait on the wire
          bought nothing.

          Read through the navigations rather than composed onto the query
          above: Project builds a ProductRow, and EF cannot correlate a
          subquery against a record it has just constructed. Folding them into
          that projection would also mean carrying a photo list on the shape
          every listing uses.
        */
        var extras = await db.Products.AsNoTracking()
            .Where(p => p.Id == row.Id)
            .Select(p => new
            {
                Gallery = p.Gallery.OrderBy(image => image.SortOrder).Select(image => image.Url).ToList(),
                Specs = p.Specs.Select(spec => new ProductSpecDto(spec.Label, spec.Value)).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var gallery = extras?.Gallery ?? [];
        var specs = extras?.Specs ?? [];

        return ToDto(row, gallery.Count > 0 ? gallery : null, specs.Count > 0 ? specs : null);
    }

    public async Task<IReadOnlyList<ProductDto>> GetRelatedProductsAsync(string slug, int limit, CancellationToken cancellationToken)
    {
        var current = await db.Products.AsNoTracking()
            .Where(p => p.Slug == slug)
            .Select(p => new { p.Id, p.CategoryId })
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            return [];
        }

        // Anything sharing any of this product's categories, not just its
        // primary one: a notebook filed under both "دفتر" and "هدیه" is related
        // to the rest of the gift shelf as much as to the rest of the notebooks.
        var siblingCategories = await db.ProductCategories.AsNoTracking()
            .Where(filing => filing.ProductId == current.Id)
            .Select(filing => filing.CategoryId)
            .ToListAsync(cancellationToken);

        // The primary column is the fallback for a product whose join rows
        // predate this table — an empty list here would return an empty shelf.
        if (siblingCategories.Count == 0)
        {
            siblingCategories.Add(current.CategoryId);
        }

        var rows = await Project(PublishedProducts()
                .Where(p => p.Id != current.Id && db.ProductCategories
                    .Any(filing => filing.ProductId == p.Id && siblingCategories.Contains(filing.CategoryId)))
                .OrderByDescending(p => p.IsBestseller)
                .ThenBy(p => p.Slug)
                .Take(Math.Clamp(limit, 1, 24)))
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => ToDto(row))];
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsBySlugsAsync(
        IReadOnlyCollection<string> slugs,
        CancellationToken cancellationToken)
    {
        if (slugs.Count == 0)
        {
            return [];
        }

        var wanted = slugs.Take(ProductQuery.MaxPageSize).ToList();
        var rows = await Project(PublishedProducts().Where(p => wanted.Contains(p.Slug)))
            .ToListAsync(cancellationToken);

        // Returned in the order asked for — the comparison screen's columns are
        // the slugs in the query string, left to right.
        return [.. wanted
            .Select(slug => rows.FirstOrDefault(row => row.Slug == slug))
            .Where(row => row is not null)
            .Select(row => ToDto(row!))];
    }

    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking()
            .Where(c => c.IsPublished)
            .Select(c => new
            {
                c.Id,
                c.Slug,
                c.Name,
                c.Icon,
                c.ImageUrl,
                c.ParentId,
                /*
                  Distinct products filed under this category *or* under any of
                  its children — which is exactly the set browsing the category
                  returns, and the reason this is one count rather than a
                  parent's own plus its children's.

                  Summing the two was right while a product had a single
                  category and cannot be now: one filed under both a parent and
                  a child of it, or under two children of the same parent, was
                  counted once per filing while the listing showed it once. The
                  tile said two products and the page under it held one.
                */
                ProductCount = db.ProductCategories
                    .Where(filing =>
                        (filing.CategoryId == c.Id
                            || db.Categories.Any(child =>
                                child.Id == filing.CategoryId && child.ParentId == c.Id))
                        && db.Products.Any(p => p.Id == filing.ProductId && p.IsPublished))
                    .Select(filing => filing.ProductId)
                    .Distinct()
                    .Count(),
            })
            .ToListAsync(cancellationToken);

        // Two levels, matching the design's tiles: a top-level category and its
        // children. Deeper nesting has no screen to appear on.
        return [.. categories
            .Where(c => c.ParentId is null)
            .Select(parent =>
            {
                var children = categories
                    .Where(c => c.ParentId == parent.Id)
                    .Select(c => new CategoryDto(c.Slug, c.Name, c.Icon, c.ProductCount, c.ImageUrl, null))
                    .ToList();

                return new CategoryDto(
                    parent.Slug,
                    parent.Name,
                    parent.Icon,
                    // Already the whole subtree, counted once per product — see
                    // the projection above. Adding the children's counts here
                    // is what made a product filed in both a parent and its
                    // child count twice.
                    parent.ProductCount,
                    parent.ImageUrl,
                    children.Count > 0 ? children : null);
            })];
    }

    public async Task<CategoryDto?> GetCategoryAsync(string slug, CancellationToken cancellationToken)
    {
        var all = await ListCategoriesAsync(cancellationToken);

        return all.FirstOrDefault(c => c.Slug == slug)
            ?? all.SelectMany(c => c.Children ?? []).FirstOrDefault(c => c.Slug == slug);
    }

    public async Task<IReadOnlyList<BrandDto>> ListBrandsAsync(CancellationToken cancellationToken) =>
        await db.Brands.AsNoTracking()
            .Where(b => b.IsPublished)
            .OrderByDescending(b => b.IsFeatured)
            .ThenBy(b => b.Name)
            .Select(b => new BrandDto(
                b.Slug,
                b.Name,
                db.Products.Count(p => p.BrandId == b.Id && p.IsPublished),
                b.Tagline,
                b.Description,
                b.LogoUrl,
                b.CoverUrl,
                b.IsFeatured ? true : null))
            .ToListAsync(cancellationToken);

    public async Task<BrandDto?> GetBrandAsync(string slug, CancellationToken cancellationToken) =>
        (await ListBrandsAsync(cancellationToken)).FirstOrDefault(b => b.Slug == slug);

    public async Task<IReadOnlyList<CollectionDto>> ListCollectionsAsync(CancellationToken cancellationToken)
    {
        var collections = await db.Collections.AsNoTracking()
            .Where(c => c.IsPublished)
            .OrderByDescending(c => c.IsFeatured)
            .ThenBy(c => c.Title)
            .Select(c => new
            {
                c.Id,
                c.Slug,
                c.Title,
                c.Summary,
                c.CoverUrl,
                c.EditorialNote,
                c.IsFeatured,
            })
            .ToListAsync(cancellationToken);

        var ids = collections.Select(c => c.Id).ToList();

        // One query for every collection's membership rather than one per
        // collection — the directory screen lists all six at once.
        var memberships = await (
            from membership in db.CollectionProducts.AsNoTracking()
            join product in db.Products.AsNoTracking() on membership.ProductId equals product.Id
            where ids.Contains(membership.CollectionId) && product.IsPublished
            orderby membership.SortOrder
            select new { membership.CollectionId, product.Slug })
            .ToListAsync(cancellationToken);

        return [.. collections.Select(c => new CollectionDto(
            c.Slug,
            c.Title,
            c.Summary,
            c.CoverUrl,
            [.. memberships.Where(m => m.CollectionId == c.Id).Select(m => m.Slug)],
            c.EditorialNote,
            c.IsFeatured ? true : null))];
    }

    public async Task<CollectionDto?> GetCollectionAsync(string slug, CancellationToken cancellationToken) =>
        (await ListCollectionsAsync(cancellationToken)).FirstOrDefault(c => c.Slug == slug);

    public async Task<IReadOnlyList<ProductDto>> GetCollectionProductsAsync(string slug, CancellationToken cancellationToken)
    {
        var ordered = await (
            from membership in db.CollectionProducts.AsNoTracking()
            join collection in db.Collections.AsNoTracking() on membership.CollectionId equals collection.Id
            join product in db.Products.AsNoTracking() on membership.ProductId equals product.Id
            // The collection's own publish state matters as well as the
            // products'. A draft collection 404s on its own page, but this list
            // is reachable by itself, and which products an unannounced
            // collection groups together is the thing worth not saying.
            where collection.Slug == slug && collection.IsPublished && product.IsPublished
            orderby membership.SortOrder
            select product.Slug)
            .ToListAsync(cancellationToken);

        return await GetProductsBySlugsAsync(ordered, cancellationToken);
    }

    public async Task<IReadOnlyList<ArticleDto>> ListArticlesAsync(string? category, CancellationToken cancellationToken)
    {
        var query = db.Articles.AsNoTracking().Where(a => a.IsPublished);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(a => a.Category == category);
        }

        // The list screens never render the body, so it is not fetched.
        return await query
            .OrderByDescending(a => a.PublishedAtUtc)
            .Select(a => new ArticleDto(
                a.Slug,
                a.Title,
                a.Excerpt,
                a.Category,
                a.CoverUrl,
                a.PublishedAtUtc,
                a.ReadingMinutes,
                a.IsFeatured ? true : null,
                null,
                a.RecommendedProductSlug))
            .ToListAsync(cancellationToken);
    }

    /// <remarks>
    /// Published pages only, and the global soft-delete filter still applies, so
    /// a page an operator archived stops being served rather than lingering
    /// because the storefront happens to ask for it by name.
    /// </remarks>
    /// <remarks>
    /// A banner with no picture is one somebody started and did not finish —
    /// the hero is a photograph with words over it, and without the photograph
    /// it is worse than the one the app ships with.
    /// </remarks>
    public Task<BannerDto?> GetBannerAsync(string slug, CancellationToken cancellationToken) =>
        db.ContentEntries.AsNoTracking()
            .Where(entry =>
                entry.Slug == slug &&
                entry.Kind == ContentKind.Banner &&
                entry.Status == ContentStatus.Published &&
                entry.CoverUrl != null &&
                entry.CoverUrl != "")
            .Select(entry => new BannerDto(
                entry.Slug,
                entry.Title,
                entry.Excerpt ?? string.Empty,
                entry.CoverUrl!))
            .FirstOrDefaultAsync(cancellationToken);

    /// <remarks>
    /// A question with no answer is one somebody started and did not finish;
    /// rendering it would put an empty accordion on the page.
    /// </remarks>
    public async Task<IReadOnlyList<FaqEntryDto>> ListFaqsAsync(CancellationToken cancellationToken) =>
        await db.ContentEntries.AsNoTracking()
            .Where(entry =>
                entry.Kind == ContentKind.Faq &&
                entry.Status == ContentStatus.Published &&
                entry.Body != null &&
                entry.Body != "")
            .OrderBy(entry => entry.Excerpt)
            .ThenBy(entry => entry.CreatedAtUtc)
            .Select(entry => new FaqEntryDto(entry.Title, entry.Body!, entry.Excerpt ?? string.Empty))
            .ToListAsync(cancellationToken);

    public Task<ContentPageDto?> GetContentPageAsync(string slug, CancellationToken cancellationToken) =>
        db.ContentEntries.AsNoTracking()
            .Where(entry =>
                entry.Slug == slug &&
                entry.Kind == ContentKind.Page &&
                entry.Status == ContentStatus.Published)
            .Select(entry => new ContentPageDto(
                entry.Slug,
                entry.Title,
                entry.Excerpt,
                entry.Body ?? string.Empty,
                entry.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ArticleDto?> GetArticleAsync(string slug, CancellationToken cancellationToken)
    {
        var article = await db.Articles.AsNoTracking()
            .Where(a => a.Slug == slug && a.IsPublished)
            .Select(a => new
            {
                a.Id,
                a.Slug,
                a.Title,
                a.Excerpt,
                a.Category,
                a.CoverUrl,
                a.PublishedAtUtc,
                a.ReadingMinutes,
                a.IsFeatured,
                a.RecommendedProductSlug,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (article is null)
        {
            return null;
        }

        var blocks = await db.ArticleBlocks.AsNoTracking()
            .Where(b => b.ArticleId == article.Id)
            .OrderBy(b => b.SortOrder)
            .Select(b => new { b.Kind, b.Text })
            .ToListAsync(cancellationToken);

        return new ArticleDto(
            article.Slug,
            article.Title,
            article.Excerpt,
            article.Category,
            article.CoverUrl,
            article.PublishedAtUtc,
            article.ReadingMinutes,
            article.IsFeatured ? true : null,
            [.. blocks.Select(b => new ArticleBlockDto(WireFormat.ArticleBlockKind(b.Kind), b.Text))],
            article.RecommendedProductSlug);
    }

    public async Task<IReadOnlyList<ProductReviewDto>> ListProductReviewsAsync(string slug, CancellationToken cancellationToken)
    {
        var rows = await (
            from review in db.ProductReviews.AsNoTracking()
            join product in db.Products.AsNoTracking() on review.ProductId equals product.Id
            // Published only: a pending or rejected review must never appear on
            // a product page, which is the whole point of the moderation state.
            where product.Slug == slug && review.Status == ModerationStatus.Published
            orderby review.CreatedAtUtc descending
            select new
            {
                review.Id,
                review.AuthorName,
                review.Rating,
                review.Body,
                review.CreatedAtUtc,
                review.IsVerifiedPurchase,
                review.HelpfulCount,
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new ProductReviewDto(
            r.Id.ToString(), r.AuthorName, r.Rating, r.Body, r.CreatedAtUtc, r.IsVerifiedPurchase, r.HelpfulCount))];
    }

    public async Task<RatingBreakdownDto> GetRatingBreakdownAsync(string slug, CancellationToken cancellationToken)
    {
        // Grouped in SQL — one row per star value, not every review pulled back
        // to be counted here.
        var counts = await (
            from review in db.ProductReviews.AsNoTracking()
            join product in db.Products.AsNoTracking() on review.ProductId equals product.Id
            where product.Slug == slug && review.Status == ModerationStatus.Published
            group review by review.Rating into stars
            select new { Rating = stars.Key, Count = stars.Count() })
            .ToListAsync(cancellationToken);

        var total = counts.Sum(c => c.Count);
        var average = total == 0 ? 0d : (double)counts.Sum(c => c.Rating * c.Count) / total;

        // Every star from 1 to 5 is present even at zero — the histogram draws
        // five bars whether or not anyone gave two stars.
        var breakdown = Enumerable.Range(1, 5)
            .ToDictionary(
                star => star.ToString(System.Globalization.CultureInfo.InvariantCulture),
                star => counts.FirstOrDefault(c => c.Rating == star)?.Count ?? 0);

        return new RatingBreakdownDto(Math.Round(average, 1), total, breakdown);
    }

    public async Task<IReadOnlyList<ProductQuestionDto>> ListProductQuestionsAsync(string slug, CancellationToken cancellationToken)
    {
        var rows = await (
            from question in db.ProductQuestions.AsNoTracking()
            join product in db.Products.AsNoTracking() on question.ProductId equals product.Id
            where product.Slug == slug && question.Status == ModerationStatus.Published
            orderby question.AskedAtUtc descending
            select new
            {
                question.Id,
                question.AuthorName,
                question.Body,
                question.AskedAtUtc,
                question.AnswerAuthor,
                question.AnswerBody,
                question.AnsweredAtUtc,
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(q => new ProductQuestionDto(
            q.Id.ToString(),
            q.AuthorName,
            q.Body,
            q.AskedAtUtc,
            q.AnswerBody is null || q.AnswerAuthor is null || q.AnsweredAtUtc is null
                ? null
                : new ProductQuestionAnswerDto(q.AnswerAuthor, q.AnswerBody, q.AnsweredAtUtc.Value)))];
    }

    /// <summary>
    /// A product's variant axes, for the storefront's option picker.
    /// </summary>
    /// <remarks>
    /// Scoped to published products, as every other read on this class is. The
    /// global filter drops archived rows but says nothing about drafts, so
    /// joining on the slug alone served an unreleased product's options to
    /// anyone who could guess its slug — and <see cref="ListSkusAsync"/>, its
    /// pair, served the prices and stock counts behind them. The product page
    /// itself already 404s for a draft; these two are reachable on their own.
    /// </remarks>
    public async Task<IReadOnlyList<ProductVariantAxisDto>> ListVariantAxesAsync(string slug, CancellationToken cancellationToken)
    {
        var axes = await (
            from axis in db.ProductVariantAxes.AsNoTracking()
            join product in db.Products.AsNoTracking() on axis.ProductId equals product.Id
            where product.Slug == slug && product.IsPublished
            orderby axis.SortOrder
            select new { axis.Id, axis.Key, axis.Label, axis.Kind })
            .ToListAsync(cancellationToken);

        if (axes.Count == 0)
        {
            return [];
        }

        var axisIds = axes.Select(a => a.Id).ToList();

        var options = await db.ProductVariantOptions.AsNoTracking()
            .Where(o => axisIds.Contains(o.AxisId))
            .OrderBy(o => o.SortOrder)
            .Select(o => new { o.AxisId, o.Key, o.Label, o.Hex, o.IsAvailable })
            .ToListAsync(cancellationToken);

        return [.. axes.Select(axis => new ProductVariantAxisDto(
            axis.Key,
            axis.Label,
            WireFormat.VariantAxisKind(axis.Kind),
            [.. options.Where(o => o.AxisId == axis.Id)
                .Select(o => new VariantOptionDto(o.Key, o.Label, o.Hex, o.IsAvailable))]))];
    }

    /// <summary>
    /// A product's sellable variants — prices and stock included, so it is
    /// scoped to published products for the reason
    /// <see cref="ListVariantAxesAsync"/> is.
    /// </summary>
    public async Task<IReadOnlyList<StorefrontSkuDto>> ListSkusAsync(string slug, CancellationToken cancellationToken) =>
        await (
            from sku in db.ProductSkus.AsNoTracking()
            join product in db.Products.AsNoTracking() on sku.ProductId equals product.Id
            where product.Slug == slug && product.IsPublished && sku.IsActive
            orderby sku.Combination
            select new StorefrontSkuDto(sku.Id.ToString(), sku.Combination, sku.Price.Amount, sku.Stock, sku.Stock > 0))
        .ToListAsync(cancellationToken);
}
