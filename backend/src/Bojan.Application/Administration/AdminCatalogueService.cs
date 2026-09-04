using Bojan.Application.Auth;
using Bojan.Application.Common;
using Bojan.Application.Notifications;
using Bojan.Application.Support;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
using Bojan.Domain.Content;
using Bojan.Domain.Inventory;
using Bojan.Domain.Marketing;
using Bojan.Domain.Orders;

namespace Bojan.Application.Administration;

/// <summary>
/// The panel's catalogue, content and inventory writes.
/// </summary>
/// <remarks>
/// <para>
/// Every method here records an audit entry through <see cref="IAuditLog"/>
/// before saving, so the change and its trail commit together —
/// <c>BACKEND.md</c> Phase 7: "Every write here goes in an audit log."
/// </para>
/// <para>
/// Role gating is not here: it is at the endpoint, where the resource's
/// declared roles live next to its route (mirroring
/// <c>apps/admin/src/lib/api/resources.ts</c>). A service that also checked
/// roles would give two places to keep in step with that file.
/// </para>
/// </remarks>
public sealed class AdminCatalogueService(
    IAdminRepository repository,
    IStockAlertRepository stockAlerts,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    ICustomerMailer mailer,
    EmailTemplates templates,
    IDateTimeProvider clock,
    IFileStorage storage)
{
    /// <summary>
    /// The folder each kind of image must have been uploaded into.
    /// </summary>
    /// <remarks>
    /// Every one of these is rendered on the storefront to every visitor, so a
    /// field that took any URL would let whoever can edit a record point the
    /// shop at an off-site host — a tracking pixel, or a <c>data:</c> payload
    /// this system never sniffed or stored. Product images were checked;
    /// a brand's logo, a collection's cover and an article's cover were not,
    /// and they are the same field with a different name.
    /// </remarks>
    private const string ProductImageFolder = "products";
    private const string BrandImageFolder = "brands";
    private const string CollectionImageFolder = "collections";
    private const string ContentImageFolder = "content";

    public async Task<UseCaseResult<string>> SaveProductAsync(SaveProductRequest request, CancellationToken cancellationToken)
    {
        Product product;

        // Images already on the product when this save started — empty for a
        // new one, since a product being created has nothing to be exempt from
        // the ownership check on. Populated from the loaded row before that
        // row's ImageUrl is touched, so a URL cannot exempt itself by having
        // just been assigned from the same request that is supplying it.
        var alreadyStoredImages = new HashSet<string>(StringComparer.Ordinal);

        // The category set, resolved once ahead of both paths below so that
        // creating and editing agree on what a valid set is. A product may be
        // filed under several categories; the first of them is the primary one.
        var categoryField = request.Categories is not null ? "categories" : "category";
        var requestedCategories = RequestedCategories(request);
        List<Category>? categories = null;

        if (requestedCategories is not null)
        {
            if (requestedCategories.Count == 0)
            {
                // Filed nowhere means gone from every listing — refused rather
                // than saved, because nothing on the form reads as asking for
                // that.
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, categoryField);
            }

            categories = await ResolveCategoriesAsync(requestedCategories, cancellationToken);
            if (categories is null)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, categoryField);
            }
        }

        if (TryParseId(request.Id, out var id))
        {
            var existingProduct = await repository.FindProductWithDetailAsync(id, cancellationToken);
            if (existingProduct is null)
            {
                return UseCaseResult<string>.Failure(UseCaseError.NotFound);
            }

            product = existingProduct;
            alreadyStoredImages.Add(product.ImageUrl);
            foreach (var url in product.Gallery.Select(image => image.Url))
            {
                alreadyStoredImages.Add(url);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "title");
            }

            var brand = await ResolveBrandAsync(request.Brand, cancellationToken);

            if (brand is null)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "brand");
            }

            if (categories is null)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, categoryField);
            }

            var desired = Slug.From(request.Title);
            var slug = await UniqueProductSlugAsync(desired, null, cancellationToken);

            product = new Product
            {
                Slug = slug,
                Title = request.Title,
                BrandId = brand.Id,
                // Overwritten a few lines down by ReplaceCategories, which is
                // what keeps the primary inside the set. Assigned here only
                // because the property is required.
                CategoryId = categories[0].Id,
                Price = new Money(request.Price ?? 0),
                // Left blank rather than taken from the request unchecked —
                // the shared block below is what validates and assigns it, and
                // assigning it here first would have made it exempt from that
                // check by definition, since it would already equal itself.
                ImageUrl = string.Empty,
                IsPublished = request.Status is null or "published",
            };

            repository.AddProduct(product);
        }

        if (request.Title is not null) product.Title = request.Title;
        if (request.Sku is not null) product.Sku = request.Sku;
        if (request.Price is { } price) product.Price = new Money(price);
        if (request.CostPrice is { } cost) product.CostPrice = new Money(cost);
        if (request.Stock is { } stock) product.Stock = stock;
        if (request.Description is not null) product.Description = request.Description;
        if (request.MetaTitle is not null) product.MetaTitle = Blank(request.MetaTitle);
        if (request.MetaDescription is not null) product.MetaDescription = Blank(request.MetaDescription);
        if (request.LowStock is { } lowStock) product.LowStockThreshold = lowStock;
        if (request.TrackStock is { } trackStock) product.TrackStock = trackStock;
        if (request.Backorder is { } backorder) product.AllowBackorder = backorder;

        // The form's own slug field wins when it is filled; the slug derived
        // from the title stands otherwise. Uniqueness is settled either way,
        // because the column is unique and a clash here is an operator typing
        // a slug another product already has, not a race.
        if (Blank(request.Slug) is { } desiredSlug)
        {
            string normalisedSlug;
            try
            {
                normalisedSlug = Slug.From(desiredSlug);
            }
            catch (ArgumentException)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "slug");
            }

            if (normalisedSlug != product.Slug)
            {
                if (await repository.ProductSlugExistsAsync(normalisedSlug, product.Id, cancellationToken))
                {
                    return UseCaseResult<string>.Failure(UseCaseError.Conflict, "slug");
                }

                product.Slug = normalisedSlug;
            }
        }

        if (request.CompareAt is { } compareAt)
        {
            // Zero clears the strike-through. Anything below the selling price
            // would render as a negative saving on the product card.
            if (compareAt != 0 && compareAt < product.Price.Amount)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "compareAt");
            }

            product.CompareAtPrice = compareAt == 0 ? null : new Money(compareAt);
        }

        if (request.Brand is not null)
        {
            var brand = await ResolveBrandAsync(request.Brand, cancellationToken);
            if (brand is null) return UseCaseResult<string>.Failure(UseCaseError.Invalid, "brand");
            product.BrandId = brand.Id;
        }

        if (categories is not null)
        {
            // Assigns the primary too — see Product.ReplaceCategories.
            product.ReplaceCategories(categories.Select(c => c.Id));
        }

        if (request.Status is { } status && !TryApplyStatus(product, status))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "status");
        }

        if (request.Images is { Count: > 0 } images)
        {
            // A URL already on this product before this save passes
            // unchecked; only a URL that is newly appearing has to be one this
            // API issued into the product folder — see the folder constants
            // for why that check is not a formality. The form resends the
            // product's whole image list on every save, including saves that
            // never touched the gallery, so checking every URL again on every
            // save meant a product whose image predates this check — every one
            // seeded from the design, which links a Google design-tool host —
            // could not be saved at all until an operator replaced its picture
            // first. New products still have every image checked, because
            // `alreadyStoredImages` is empty for one of those.
            if (images.Any(url => !alreadyStoredImages.Contains(url) && !storage.IsOwnUrl(url, ProductImageFolder)))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "images");
            }

            // First is the primary; the rest are the gallery, replaced rather
            // than appended so a removal on screen 105 is a removal here. The
            // list arrived complete and in order, and storing only images[0]
            // was silently discarding the rest of it.
            product.ImageUrl = images[0];
            product.ReplaceGallery(images.Skip(1));
        }

        if (request.Collections is { } requestedCollections)
        {
            if (!await ApplyCollectionsAsync(product, requestedCollections, cancellationToken))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "collections");
            }
        }

        /*
            Nothing leaves here priced at nothing.

            A product at zero with no list price above it is one nobody priced,
            and published it is a product the shop gives away. The exception is
            the one an operator asks for on purpose — zero against a
            `CompareAtPrice` is a hundred-percent discount — which is why the
            rule reads the pair rather than the price alone. Combinations are
            held to the same rule in SaveSkusAsync.

            Checked at the end rather than beside the assignment, because the
            price, the compare-at and the discount that sets both can each
            arrive in this one request, and only their result is a fact about
            the product.
        */
        if (product.Price == Money.Zero && product.CompareAtPrice is null)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "price-required");
        }

        audit.Record("product.saved", product.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return product.Id.ToString();
    }

    /// <summary>
    /// Puts a product in one of the three states screen 96's filter names, or
    /// answers <c>false</c> for a value that is none of them.
    /// </summary>
    /// <remarks>
    /// "archived" is the soft delete rather than a third boolean — see
    /// <c>WireFormat.ProductStatus</c> for why the state is derived from the
    /// deleted stamp and the published flag together. Shared by the product
    /// form and the list's tick-boxes so the two cannot drift: the same word
    /// has to mean the same thing whether it arrives from a save of one
    /// product or a batch of twenty.
    /// </remarks>
    private bool TryApplyStatus(Product product, string status)
    {
        switch (status)
        {
            case "archived":
                product.SoftDelete(clock.UtcNow);
                return true;
            case "published":
                product.Restore();
                product.IsPublished = true;
                return true;
            case "draft":
                product.Restore();
                product.IsPublished = false;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Screen 96's tick-boxes — draft, published or archived, applied to
    /// everything selected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing here can change a price, a category or a picture: the request
    /// carries the ids and one word, and this only calls
    /// <see cref="TryApplyStatus"/>. That is the whole reason it is not a loop
    /// over <see cref="SaveProductAsync"/>.
    /// </para>
    /// <para>
    /// An id that resolves to nothing is skipped rather than failing the
    /// batch, because an operator's page is a snapshot and a product deleted
    /// from another tab in the meantime is not a reason to refuse the other
    /// nineteen. The count comes back so the panel can say what actually
    /// happened; a batch where nothing at all resolved is a 404, since that is
    /// a page too stale to act on.
    /// </para>
    /// <para>
    /// One audit row per product, not per batch. The trail is read to answer
    /// "who archived this?", and a single row naming twenty slugs answers it
    /// for none of them.
    /// </para>
    /// </remarks>
    public async Task<UseCaseResult<BulkProductResultDto>> SetProductsStatusAsync(
        BulkProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Ids is not { Count: > 0 })
        {
            return UseCaseResult<BulkProductResultDto>.Failure(UseCaseError.Invalid, "ids");
        }

        var (slugs, ids) = SplitReferences(request.Ids);
        var products = await repository.FindProductsAsync(slugs, ids, cancellationToken);

        if (products.Count == 0)
        {
            return UseCaseResult<BulkProductResultDto>.Failure(UseCaseError.NotFound);
        }

        foreach (var product in products)
        {
            if (!TryApplyStatus(product, request.Status))
            {
                // Asked here rather than up front so that TryApplyStatus stays
                // the only definition of a valid status; it refuses without
                // touching the product, and the answer cannot differ between
                // them, so the batch is turned away before anything is saved.
                return UseCaseResult<BulkProductResultDto>.Failure(UseCaseError.Invalid, "status");
            }

            audit.Record($"product.{request.Status}", product.Slug);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new BulkProductResultDto(products.Count, []);
    }

    /// <summary>
    /// Removing products from screen 96 outright.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Refused for a product that has ever been ordered, counted in a stocktake
    /// or returned. Those three are the references the schema will not cascade,
    /// and for the same reason a customer with orders is not deleted: an
    /// invoice whose product has been deleted is not a tidier invoice. Those
    /// products get archived, which is what the button beside this one does.
    /// </para>
    /// <para>
    /// Partial rather than all-or-nothing. An operator ticking twenty products
    /// where one of them sold a unit last spring is asking for the other
    /// nineteen to go, and a batch that refused the lot would leave them
    /// deleting one at a time to find the one that is stuck. The ones that were
    /// kept come back by name, because "۱ محصول حذف نشد" on a page of twenty
    /// says nothing an operator can act on.
    /// </para>
    /// <para>
    /// A batch where every product was blocked is a refusal rather than a
    /// success that changed nothing — the operator asked for a deletion and got
    /// none, and the panel has a sentence for it.
    /// </para>
    /// </remarks>
    public async Task<UseCaseResult<BulkProductResultDto>> DeleteProductsAsync(
        DeleteProductsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Ids is not { Count: > 0 })
        {
            return UseCaseResult<BulkProductResultDto>.Failure(UseCaseError.Invalid, "ids");
        }

        var (slugs, ids) = SplitReferences(request.Ids);
        var products = await repository.FindProductsAsync(slugs, ids, cancellationToken);

        if (products.Count == 0)
        {
            return UseCaseResult<BulkProductResultDto>.Failure(UseCaseError.NotFound);
        }

        var traded = new HashSet<Guid>(await repository.ProductsWithTradingHistoryAsync(
            [.. products.Select(product => product.Id)],
            cancellationToken));

        var blocked = new List<string>();
        var removed = 0;

        foreach (var product in products)
        {
            if (traded.Contains(product.Id))
            {
                blocked.Add(product.Title);
                continue;
            }

            // Recorded before the row goes, because afterwards there is no slug
            // to record it against.
            audit.Record("product.deleted", product.Slug);
            repository.RemoveProduct(product);
            removed++;
        }

        if (removed == 0)
        {
            return UseCaseResult<BulkProductResultDto>.Failure(
                UseCaseError.Conflict,
                "product-has-history");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new BulkProductResultDto(removed, blocked);
    }

    /// <summary>
    /// The category values the request is asking for, or <c>null</c> when it
    /// asks for none and the product's current filing should stand.
    /// </summary>
    /// <remarks>
    /// <c>categories</c> wins over <c>category</c> when both arrive: the list
    /// carries everything the single field does and more, so honouring the
    /// single one on top of it could only narrow what the operator picked.
    /// </remarks>
    private static List<string>? RequestedCategories(SaveProductRequest request)
    {
        if (request.Categories is { } many)
        {
            return [.. many.Where(value => !string.IsNullOrWhiteSpace(value))];
        }

        if (request.Category is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(request.Category) ? [] : [request.Category];
    }

    /// <summary>
    /// Resolves every value to a category, in order, or <c>null</c> if any of
    /// them names nothing.
    /// </summary>
    /// <remarks>
    /// All or nothing: a save that quietly dropped the one slug it could not
    /// find would file the product under a smaller set than the operator ticked
    /// and report success.
    /// </remarks>
    private async Task<List<Category>?> ResolveCategoriesAsync(
        IReadOnlyList<string> values,
        CancellationToken cancellationToken)
    {
        var (slugs, ids) = SplitReferences(values);
        var found = await repository.FindCategoriesAsync(slugs, ids, cancellationToken);

        return Reorder(values, found, category => category.Slug, category => category.Id);
    }

    /// <summary>
    /// Sorts a set of references into the two things they can be.
    /// </summary>
    /// <remarks>
    /// The panel posts slugs and an import posts ids, and a single request may
    /// carry both. Splitting them is what lets the whole set be read in one
    /// statement rather than one per element.
    /// </remarks>
    private static (List<string> Slugs, List<Guid> Ids) SplitReferences(IEnumerable<string> values)
    {
        var slugs = new List<string>();
        var ids = new List<Guid>();

        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (TryParseId(value, out var id))
            {
                ids.Add(id);
            }
            else
            {
                slugs.Add(value);
            }
        }

        return (slugs, ids);
    }

    /// <summary>
    /// Puts what was found back into the order it was asked for, or
    /// <c>null</c> if any reference matched nothing.
    /// </summary>
    /// <remarks>
    /// All or nothing: a save that quietly dropped the one slug it could not
    /// find would store a smaller set than the operator arranged and report
    /// success. Order is the caller's, not the database's — for categories
    /// position zero is the primary one, and for a collection it is what the
    /// storefront renders.
    /// </remarks>
    private static List<T>? Reorder<T>(
        IReadOnlyList<string> values,
        IReadOnlyList<T> found,
        Func<T, string> slugOf,
        Func<T, Guid> idOf)
    {
        var resolved = new List<T>();

        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var match = TryParseId(value, out var id)
                ? found.FirstOrDefault(row => idOf(row) == id)
                : found.FirstOrDefault(row => slugOf(row) == value);

            if (match is null)
            {
                return null;
            }

            // The same thing named twice is one membership, and the first
            // mention decides where it sits.
            if (resolved.TrueForAll(existing => idOf(existing) != idOf(match)))
            {
                resolved.Add(match);
            }
        }

        return resolved;
    }

    /// <summary>
    /// Puts the product in exactly the collections named, and no others.
    /// </summary>
    /// <remarks>
    /// Only the difference is written. A membership the product already has
    /// keeps the position an operator arranged it in, and one it is joining
    /// lands at the end of that collection rather than at position zero, where
    /// it would tie with whatever is already first and then order arbitrarily.
    /// </remarks>
    private async Task<bool> ApplyCollectionsAsync(
        Product product,
        IReadOnlyList<string> values,
        CancellationToken cancellationToken)
    {
        var (slugs, ids) = SplitReferences(values);
        var found = await repository.FindCollectionsAsync(slugs, ids, cancellationToken);
        var resolved = Reorder(values, found, collection => collection.Slug, collection => collection.Id);

        if (resolved is null)
        {
            return false;
        }

        var wanted = resolved.ConvertAll(collection => collection.Id);

        var existing = await repository.ListProductMembershipsAsync(product.Id, cancellationToken);
        var removed = existing.Where(membership => !wanted.Contains(membership.CollectionId)).ToList();
        var joining = wanted.Where(id => existing.All(membership => membership.CollectionId != id)).ToList();

        var added = new List<CollectionProduct>();

        if (joining.Count > 0)
        {
            var siblings = await repository.ListCollectionMembershipsAsync(joining, cancellationToken);

            foreach (var collectionId in joining)
            {
                var nextSortOrder = siblings
                    .Where(membership => membership.CollectionId == collectionId)
                    .Select(membership => membership.SortOrder + 1)
                    .DefaultIfEmpty(0)
                    .Max();

                added.Add(new CollectionProduct
                {
                    CollectionId = collectionId,
                    ProductId = product.Id,
                    SortOrder = nextSortOrder,
                });
            }
        }

        repository.ReplaceProductMemberships(removed, added);
        return true;
    }

    /// <summary>
    /// The pricing screen. This is the one write that may set
    /// <c>costPrice</c>, and it is why that field exists on
    /// <see cref="Product"/> at all — it must never reach a storefront
    /// response (<c>BACKEND.md</c> Phase 7).
    /// </summary>
    public async Task<UseCaseResult> UpdatePricingAsync(ProductPricingRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseId(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        var product = await repository.FindProductAsync(id, cancellationToken);
        if (product is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        if (request.Price is { } price && price < 0)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "price");
        }

        if (request.CostPrice is { } cost && cost < 0)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "costPrice");
        }

        // Resolved before anything is assigned, so the compare-at rule below is
        // checked against the state the product will actually be left in. The
        // fields are optional and independent, and validating each as it landed
        // meant a request carrying only a higher `price` was never checked
        // against the compare-at already on the row — the product card then
        // struck through a number *lower* than the one being charged, which is
        // the negative saving this rule exists to prevent. The form sends the
        // fields it shows, so which of them is present is the caller's choice,
        // not something to depend on.
        var finalPrice = request.Price is { } newPrice ? new Money(newPrice) : product.Price;

        var finalCompareAt = request.CompareAtPrice is { } compareAt
            ? (compareAt == 0 ? null : new Money(compareAt))
            : product.CompareAtPrice;

        if (finalCompareAt is { } listPrice && listPrice < finalPrice)
        {
            // Named for the field the operator can act on: when the request
            // carries a compare-at it is that value being refused, and when it
            // does not, the price is what would break the pair.
            return UseCaseResult.Failure(
                UseCaseError.Invalid,
                request.CompareAtPrice is not null ? "compareAtPrice" : "price");
        }

        product.Price = finalPrice;
        product.CompareAtPrice = finalCompareAt;

        if (request.CostPrice is { } costPrice)
        {
            product.CostPrice = new Money(costPrice);
        }

        audit.Record("product.pricing.updated", product.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// The discount screen. A discount is expressed as the pair the product
    /// card already renders — <c>compareAtPrice</c> struck through, and
    /// <c>price</c> beside it — rather than as a third field, so nothing has to
    /// recompute a sale price at read time.
    /// </summary>
    public async Task<UseCaseResult> ApplyDiscountAsync(ProductDiscountRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseId(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        var product = await repository.FindProductAsync(id, cancellationToken);
        if (product is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        var listPrice = product.CompareAtPrice ?? product.Price;

        var discounted = request switch
        {
            { Percent: { } percent } when percent is > 0 and < 100 =>
                new Money(listPrice.Amount - (listPrice.Amount * percent / 100)),
            { Amount: { } amount } when amount > 0 && amount < listPrice.Amount =>
                listPrice.ClampedMinus(new Money(amount)),
            // Clearing the discount: no percent and no amount restores the list price.
            { Percent: null or 0, Amount: null or 0 } => listPrice,
            _ => Money.Zero,
        };

        if (discounted == Money.Zero)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "discount");
        }

        if (discounted == listPrice)
        {
            product.Price = listPrice;
            product.CompareAtPrice = null;
        }
        else
        {
            product.CompareAtPrice = listPrice;
            product.Price = discounted;
        }

        audit.Record("product.discount.applied", product.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    public async Task<UseCaseResult<string>> SaveCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        Category category;

        if (TryParseId(request.Id, out var id))
        {
            var existing = await repository.FindCategoryAsync(id, cancellationToken);
            if (existing is null) return UseCaseResult<string>.Failure(UseCaseError.NotFound);
            category = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "title");
            }

            category = new Category
            {
                Slug = request.Slug ?? Slug.From(request.Title),
                Name = request.Title,
            };
            repository.AddCategory(category);
        }

        if (request.Title is not null) category.Name = request.Title;
        if (request.Slug is not null) category.Slug = request.Slug;
        if (request.Icon is not null) category.Icon = request.Icon;
        if (request.MetaTitle is not null) category.MetaTitle = Blank(request.MetaTitle);
        if (request.MetaDescription is not null) category.MetaDescription = Blank(request.MetaDescription);
        if (request.ShowInMenu is { } showInMenu) category.ShowInMenu = showInMenu;
        if (request.Order is { } order) category.SortOrder = order;

        if (request.ParentId is { } parentId)
        {
            if (parentId.Length == 0)
            {
                category.ParentId = null;
            }
            else if (TryParseId(parentId, out var parent))
            {
                // A category that is its own parent would loop the tile tree.
                if (parent == category.Id) return UseCaseResult<string>.Failure(UseCaseError.Invalid, "parentId");
                category.ParentId = parent;
            }
            else
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "parentId");
            }
        }

        ApplyPublishState(category, request.Status);

        audit.Record("category.saved", category.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return category.Id.ToString();
    }

    public async Task<UseCaseResult<string>> SaveBrandAsync(SaveBrandRequest request, CancellationToken cancellationToken)
    {
        Brand brand;

        if (TryParseId(request.Id, out var id))
        {
            var existing = await repository.FindBrandAsync(id, cancellationToken);
            if (existing is null) return UseCaseResult<string>.Failure(UseCaseError.NotFound);
            brand = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "title");
            }

            brand = new Brand { Slug = request.Slug ?? Slug.From(request.Title), Name = request.Title };
            repository.AddBrand(brand);
        }

        if (request.Title is not null) brand.Name = request.Title;
        if (request.Slug is not null) brand.Slug = request.Slug;
        if (request.Tagline is not null) brand.Tagline = Blank(request.Tagline);
        if (request.Country is not null) brand.Country = Blank(request.Country);
        if (request.Description is not null) brand.Description = request.Description;
        if (request.MetaTitle is not null) brand.MetaTitle = Blank(request.MetaTitle);
        if (request.MetaDescription is not null) brand.MetaDescription = Blank(request.MetaDescription);
        if (request.Featured is { } featured) brand.IsFeatured = featured;

        if (request.Logo is not null)
        {
            // Unchanged from what the brand already had is exempt from the
            // ownership check — see the note on the same pattern in
            // SaveProductAsync. The form resends this field on every save, so
            // without the exemption a brand seeded with a design-tool URL
            // (every one of them) could not be saved at all until an operator
            // replaced its logo first.
            if (request.Logo != brand.LogoUrl && !IsStorableImage(request.Logo, BrandImageFolder))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "logo");
            }

            brand.LogoUrl = Blank(request.Logo);
        }

        ApplyPublishState(brand, request.Status);

        audit.Record("brand.saved", brand.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return brand.Id.ToString();
    }

    public async Task<UseCaseResult<string>> SaveCollectionAsync(SaveCollectionRequest request, CancellationToken cancellationToken)
    {
        Collection collection;

        if (TryParseId(request.Id, out var id))
        {
            var existing = await repository.FindCollectionAsync(id, cancellationToken);
            if (existing is null) return UseCaseResult<string>.Failure(UseCaseError.NotFound);
            collection = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "title");
            }

            collection = new Collection { Slug = request.Slug ?? Slug.From(request.Title), Title = request.Title };
            repository.AddCollection(collection);
        }

        if (request.Title is not null) collection.Title = request.Title;
        if (request.Slug is not null) collection.Slug = request.Slug;
        // The form has grown a dedicated summary field; `description` is what
        // the shared entity form still calls it, so the explicit one wins and
        // the other remains the fallback rather than being ignored.
        if (request.Description is not null) collection.Summary = request.Description;
        if (request.Summary is not null) collection.Summary = request.Summary;
        if (request.EditorialNote is not null) collection.EditorialNote = Blank(request.EditorialNote);
        if (request.Featured is { } featured) collection.IsFeatured = featured;

        if (request.Cover is not null)
        {
            // Same exemption as SaveProductAsync's images and
            // SaveBrandAsync's logo: unchanged from what is already stored
            // passes without the check.
            if (request.Cover != collection.CoverUrl && !IsStorableImage(request.Cover, CollectionImageFolder))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "cover");
            }

            collection.CoverUrl = request.Cover;
        }

        ApplyPublishState(collection, request.Status);

        audit.Record("collection.saved", collection.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return collection.Id.ToString();
    }

    /// <summary>
    /// Sets what a collection holds and the order it holds it in.
    /// </summary>
    /// <remarks>
    /// The mirror of the product form's own collections field, and deliberately
    /// a different write: this one loads the collection and replaces its whole
    /// membership, because ordering is the thing the screen exists for and
    /// order is only expressible over the complete list. The product side
    /// touches several collections at once and never loads any of them —
    /// reading N collections to add one row to each would be the wrong shape
    /// for that direction.
    /// </remarks>
    public async Task<UseCaseResult> SaveCollectionProductsAsync(
        SaveCollectionProductsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseId(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        var collection = await repository.FindCollectionAsync(id, cancellationToken);
        if (collection is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        var (slugs, ids) = SplitReferences(request.Products);
        var found = await repository.FindProductsAsync(slugs, ids, cancellationToken);
        var resolved = Reorder(request.Products, found, product => product.Slug, product => product.Id);

        if (resolved is null)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "products");
        }

        collection.ReplaceProducts(resolved.Select(product => product.Id));

        audit.Record("collection.products.saved", collection.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    public async Task<UseCaseResult<string>> SaveContentAsync(SaveContentRequest request, CancellationToken cancellationToken)
    {
        ContentEntry entry;

        if (TryParseId(request.Id, out var id))
        {
            var existing = await repository.FindContentAsync(id, cancellationToken);
            if (existing is null) return UseCaseResult<string>.Failure(UseCaseError.NotFound);
            entry = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "title");
            }

            if (!Enum.TryParse<ContentKind>(request.Kind, ignoreCase: true, out var kind))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "kind");
            }

            entry = new ContentEntry
            {
                Slug = request.Slug ?? Slug.From(request.Title),
                Title = request.Title,
                Kind = kind,
            };
            repository.AddContent(entry);
        }

        if (request.Title is not null) entry.Title = request.Title;
        if (request.Slug is not null) entry.Slug = request.Slug;
        if (request.Body is not null) entry.Body = request.Body;
        if (request.Excerpt is not null) entry.Excerpt = request.Excerpt;

        if (request.Cover is not null)
        {
            // Same exemption as the other three image fields in this file.
            if (request.Cover != entry.CoverUrl && !IsStorableImage(request.Cover, ContentImageFolder))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "cover");
            }

            entry.CoverUrl = Blank(request.Cover);
        }

        if (request.Kind is not null)
        {
            if (!Enum.TryParse<ContentKind>(request.Kind, ignoreCase: true, out var kind))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "kind");
            }

            entry.Kind = kind;
        }

        if (request.Status is not null)
        {
            if (request.Status == "archived")
            {
                entry.SoftDelete(clock.UtcNow);
            }
            else if (Enum.TryParse<ContentStatus>(request.Status, ignoreCase: true, out var status))
            {
                entry.Restore();
                entry.Status = status;
            }
            else
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "status");
            }
        }

        entry.UpdatedAtUtc = clock.UtcNow;

        audit.Record("content.saved", entry.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entry.Id.ToString();
    }

    public async Task<UseCaseResult<string>> SaveCampaignAsync(SaveCampaignRequest request, CancellationToken cancellationToken)
    {
        Campaign campaign;

        if (TryParseId(request.Id, out var id))
        {
            var existing = await repository.FindCampaignAsync(id, cancellationToken);
            if (existing is null) return UseCaseResult<string>.Failure(UseCaseError.NotFound);
            campaign = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "title");
            }

            if (!Enum.TryParse<CampaignKind>(request.Kind, ignoreCase: true, out var kind))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "kind");
            }

            campaign = new Campaign { Title = request.Title, Kind = kind };
            repository.AddCampaign(campaign);
        }

        if (request.Title is not null) campaign.Title = request.Title;
        if (request.Description is not null) campaign.Description = request.Description;
        if (request.StartsAt is { } startsAt) campaign.StartsAtUtc = startsAt;
        if (request.EndsAt is { } endsAt) campaign.EndsAtUtc = endsAt;

        if (request.Kind is not null)
        {
            if (!Enum.TryParse<CampaignKind>(request.Kind, ignoreCase: true, out var kind))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "kind");
            }

            campaign.Kind = kind;
        }

        if (request.Status is not null)
        {
            /*
                `archived` soft-deletes, exactly as it does for a content entry.
                `Campaign` has been a `SoftDeletableEntity` since it was written
                and nothing ever called either method on it — so a campaign
                created by mistake, or one whose season is over and which should
                stop appearing anywhere, could not be removed by any route the
                panel had. Its three statuses describe when it runs, not whether
                it exists, and "ended" is a campaign that ran, which is not the
                same thing as one that should never have been.
            */
            if (request.Status == "archived")
            {
                campaign.SoftDelete(clock.UtcNow);
            }
            else if (Enum.TryParse<CampaignStatus>(request.Status, ignoreCase: true, out var status))
            {
                campaign.Restore();
                campaign.Status = status;
            }
            else
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "status");
            }
        }

        if (campaign.StartsAtUtc is { } from && campaign.EndsAtUtc is { } to && to < from)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "endsAt");
        }

        audit.Record("campaign.saved", campaign.Title);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return campaign.Id.ToString();
    }

    public async Task<UseCaseResult<string>> SaveCouponAsync(SaveCouponRequest request, CancellationToken cancellationToken)
    {
        Coupon coupon;

        if (TryParseId(request.Id, out var id))
        {
            var existing = await repository.FindCouponAsync(id, cancellationToken);
            if (existing is null) return UseCaseResult<string>.Failure(UseCaseError.NotFound);
            coupon = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "code");
            }

            var code = request.Code.Trim().ToUpperInvariant();
            if (await repository.FindCouponByCodeAsync(code, cancellationToken) is not null)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Conflict, "code");
            }

            coupon = new Coupon { Code = code };
            repository.AddCoupon(coupon);
        }

        if (request.Code is not null)
        {
            var code = request.Code.Trim().ToUpperInvariant();

            if (code.Length == 0)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "code");
            }

            if (code != coupon.Code)
            {
                // An order records the code it was placed with as text, and the
                // per-customer "already used" check matches on that text. Renaming
                // a coupon that has been redeemed would orphan every one of those
                // rows, and each customer who had used it could use it again.
                if (coupon.RedemptionCount > 0)
                {
                    return UseCaseResult<string>.Failure(UseCaseError.Conflict, "code-redeemed");
                }

                // Checked rather than left to the unique index, which would
                // surface as a 500 instead of a field error the form can point at.
                if (await repository.FindCouponByCodeAsync(code, cancellationToken) is not null)
                {
                    return UseCaseResult<string>.Failure(UseCaseError.Conflict, "code");
                }

                coupon.Code = code;
            }
        }

        if (request.ExpiresAt is { } expiresAt) coupon.ExpiresAtUtc = expiresAt;
        if (request.MinimumSpend is { } minimum) coupon.MinimumSpend = minimum > 0 ? new Money(minimum) : null;
        if (request.Status is not null) coupon.IsActive = request.Status == "active";

        // A coupon is a percentage or a fixed amount, never both — Coupon.Validate
        // reads PercentOff first, so leaving a stale AmountOff behind would be
        // silently ignored today and wrong the moment the percent is cleared.
        if (request.Percent is { } percent && percent > 0)
        {
            if (percent >= 100) return UseCaseResult<string>.Failure(UseCaseError.Invalid, "percent");
            coupon.PercentOff = percent;
            coupon.AmountOff = null;
        }
        else if (request.Amount is { } amount && amount > 0)
        {
            coupon.AmountOff = new Money(amount);
            coupon.PercentOff = null;
        }

        if (coupon.PercentOff is null && coupon.AmountOff is null)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "discount");
        }

        audit.Record("coupon.saved", coupon.Code);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return coupon.Id.ToString();
    }

    /// <summary>
    /// Records a stock movement and applies it in the same save, so the
    /// running count and its reason can never disagree.
    /// </summary>
    /// <remarks>
    /// Inside a transaction, against a locked product row. Every branch below
    /// reads <c>Stock</c> and then writes it — "in" and "out" add to what they
    /// read, and "out" refuses on what they read — so without the lock two
    /// operators working the same product, or one operator and one shopper
    /// checking out, each write a count computed from a figure the other has
    /// already changed. The movement rows would still list both; only the
    /// running total would be wrong, which is the version of this bug that takes
    /// a stocktake to notice.
    /// </remarks>
    public Task<UseCaseResult> RecordStockMovementAsync(
        Guid actorId,
        StockMovementRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseId(request.ProductId, out var productId))
        {
            return Task.FromResult(UseCaseResult.Failure(UseCaseError.Invalid, "productId"));
        }

        if (!Enum.TryParse<StockMovementKind>(request.Kind, ignoreCase: true, out var kind))
        {
            return Task.FromResult(UseCaseResult.Failure(UseCaseError.Invalid, "kind"));
        }

        if (request.Quantity < 0)
        {
            return Task.FromResult(UseCaseResult.Failure(UseCaseError.Invalid, "quantity"));
        }

        return unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var product = await repository.FindProductForUpdateAsync(productId, token);
                if (product is null)
                {
                    return UseCaseResult.Failure(UseCaseError.NotFound);
                }

                // Read before the movement is applied, because "came back into
                // stock" is a transition, not a state: a delivery into a
                // product that already had ten on the shelf is not news to
                // anyone waiting.
                var wasOutOfStock = product.Stock <= 0;

                switch (kind)
                {
                    case StockMovementKind.In:
                        product.IncreaseStock(request.Quantity);
                        break;
                    case StockMovementKind.Out when request.Quantity > product.Stock:
                        return UseCaseResult.Failure(UseCaseError.Invalid, "quantity");
                    case StockMovementKind.Out:
                        product.ReduceStock(request.Quantity);
                        break;
                    case StockMovementKind.Adjust:
                        // A stocktake sets the count outright rather than adding to it.
                        product.Stock = request.Quantity;
                        break;
                    default:
                        return UseCaseResult.Failure(UseCaseError.Invalid, "kind");
                }

                repository.AddStockMovement(new StockMovement
                {
                    ProductId = productId,
                    Kind = kind,
                    Quantity = request.Quantity,
                    Reason = request.Reason,
                    Reference = request.Reference,
                    ActorId = actorId,
                    AtUtc = clock.UtcNow,
                });

                audit.Record(
                    $"inventory.{request.Kind.ToLowerInvariant()}",
                    product.Sku is { Length: > 0 } sku ? sku : product.Slug);

                await unitOfWork.SaveChangesAsync(token);

                // Everyone who asked to be told, told — once. `NotifiedAtUtc`
                // has been on the entity since it was written and nothing ever
                // set it, so every one of these requests was collected and then
                // ignored.
                if (wasOutOfStock && product.Stock > 0)
                {
                    var waiting = await stockAlerts.ListPendingAsync(product.Id, token);
                    if (waiting.Count > 0)
                    {
                        var message = templates.BackInStock(product.Title, product.Slug, product.Price.Amount);

                        foreach (var alert in waiting)
                        {
                            await mailer.SendAsync(alert.Email, message, token);

                            // Stamped whether or not the mail got through: the
                            // mailer swallows failures, and a restock that
                            // retried every past request would mail the same
                            // person on every delivery.
                            alert.NotifiedAtUtc = clock.UtcNow;
                        }

                        await unitOfWork.SaveChangesAsync(token);
                    }
                }

                return UseCaseResult.Success();
            },
            cancellationToken);
    }

    private void ApplyPublishState(SoftDeletableEntity entity, string? status)
    {
        switch (status)
        {
            case null:
                return;
            case "archived":
                entity.SoftDelete(clock.UtcNow);
                return;
            default:
                entity.Restore();
                break;
        }

        var published = status != "draft";

        switch (entity)
        {
            case Category category: category.IsPublished = published; break;
            case Brand brand: brand.IsPublished = published; break;
            case Collection collection: collection.IsPublished = published; break;
            case Product product: product.IsPublished = published; break;
            default: break;
        }
    }

    private async Task<Brand?> ResolveBrandAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // The form sends whichever it has — the panel's product form posts a
        // slug, an import might post an id.
        return TryParseId(value, out var id)
            ? await repository.FindBrandAsync(id, cancellationToken)
            : await repository.FindBrandBySlugAsync(value, cancellationToken);
    }

    /// <summary>
    /// The desired slug, or the first free <c>-2</c>, <c>-3</c>… beside it.
    /// </summary>
    /// <remarks>
    /// The whole family is read once and the suffix chosen from it. Probing the
    /// database per candidate meant a title already used twenty times cost
    /// twenty round trips, and one used a thousand times raised rather than
    /// answered — while every one of those answers sits in a single range of
    /// the unique index on <c>Slug</c>.
    /// </remarks>
    private async Task<string> UniqueProductSlugAsync(string desired, Guid? exceptId, CancellationToken cancellationToken)
    {
        var taken = (await repository.ProductSlugFamilyAsync(desired, exceptId, cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        if (!taken.Contains(desired))
        {
            return desired;
        }

        // Unbounded on purpose. The old ceiling of 998 was not a safety limit —
        // it was where a save started throwing instead of saving, and the shop
        // that reaches it is the one selling well.
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{desired}-{suffix}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    // --- screens 106, 107, 108 ---------------------------------------------

    /// <summary>How many rows any one of the three screens may hold.</summary>
    /// <remarks>
    /// Well above what a real product needs, and low enough that a crafted body
    /// cannot turn one save into thousands of inserts.
    /// </remarks>
    private const int MaxRows = 100;

    private const int MaxOptionsPerAxis = 50;

    /// <summary>
    /// Screen 107 — replaces the product's variant axes with what was posted.
    /// </summary>
    /// <remarks>
    /// Whole-list, not per-row: the screen edits a matrix, and a removal has to
    /// remove. The keys are what SKUs reference, so they are normalised to a
    /// stable lowercase form rather than taken as typed.
    /// </remarks>
    public async Task<UseCaseResult> SaveVariantsAsync(
        SaveVariantsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseId(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        var product = await repository.FindProductAsync(id, cancellationToken);
        if (product is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        if (request.Axes.Count > MaxRows)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "axes");
        }

        var axes = new List<ProductVariantAxis>(request.Axes.Count);
        var seenAxisKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 0;

        foreach (var incoming in request.Axes)
        {
            var key = NormaliseKey(incoming.Key);
            if (key.Length == 0 || string.IsNullOrWhiteSpace(incoming.Label))
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "axis");
            }

            // Two axes with one key would make a combination ambiguous about
            // which axis it named.
            if (!seenAxisKeys.Add(key))
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "axis-duplicate");
            }

            if (incoming.Options.Count is 0 or > MaxOptionsPerAxis)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "options");
            }

            var kind = incoming.Kind?.ToLowerInvariant() switch
            {
                "swatch" => VariantAxisKind.Swatch,
                null or "" or "chip" => VariantAxisKind.Chip,
                _ => (VariantAxisKind?)null,
            };

            if (kind is null)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "kind");
            }

            var axis = new ProductVariantAxis
            {
                ProductId = product.Id,
                Key = key,
                Label = incoming.Label.Trim(),
                Kind = kind.Value,
                SortOrder = order,
            };

            var seenOptionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var optionOrder = 0;

            foreach (var option in incoming.Options)
            {
                var optionKey = NormaliseKey(option.Key);
                if (optionKey.Length == 0 || string.IsNullOrWhiteSpace(option.Label))
                {
                    return UseCaseResult.Failure(UseCaseError.Invalid, "option");
                }

                if (!seenOptionKeys.Add(optionKey))
                {
                    return UseCaseResult.Failure(UseCaseError.Invalid, "option-duplicate");
                }

                // A swatch draws a colour dot, so it needs one; a chip must not
                // carry a stray hex that nothing renders.
                var hex = kind == VariantAxisKind.Swatch ? option.Hex?.Trim() : null;
                if (kind == VariantAxisKind.Swatch && !IsHexColour(hex))
                {
                    return UseCaseResult.Failure(UseCaseError.Invalid, "hex");
                }

                axis.AddOption(optionKey, option.Label.Trim(), hex, option.Available ?? true, optionOrder);
                optionOrder++;
            }

            axes.Add(axis);
            order++;
        }

        var existing = await repository.ListVariantAxesAsync(product.Id, cancellationToken);
        repository.ReplaceVariantAxes(product.Id, existing, axes);

        audit.Record("product.variants.saved", product.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// Screen 108 — replaces the product's SKUs with what was posted.
    /// </summary>
    /// <summary>
    /// One posted SKU row, validated and normalised, before it is matched
    /// against what the product already has.
    /// </summary>
    /// <remarks>
    /// A plain carrier rather than a <see cref="ProductSku"/>: building the
    /// entity up front is what made the old code delete and re-add the whole
    /// set, because it had a new row in hand before it knew whether the
    /// product already had one under that code.
    /// </remarks>
    private sealed record IncomingSku(
        string Code,
        string? Barcode,
        string Combination,
        Money Price,
        int Stock,
        bool IsActive,
        Money? CompareAtPrice);

    public async Task<UseCaseResult> SaveSkusAsync(
        SaveSkusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseId(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        var product = await repository.FindProductAsync(id, cancellationToken);
        if (product is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        if (request.Skus.Count > MaxRows)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "skus");
        }

        var skus = new List<IncomingSku>(request.Skus.Count);
        var codes = new List<string>(request.Skus.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var incoming in request.Skus)
        {
            var code = incoming.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            if (code.Length is 0 or > 64)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "code");
            }

            if (!seen.Add(code))
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "code-duplicate");
            }

            var price = incoming.Price ?? product.Price.Amount;
            var stock = incoming.Stock ?? 0;
            var compareAt = incoming.CompareAt ?? 0;

            if (price < 0) return UseCaseResult.Failure(UseCaseError.Invalid, "price");
            if (stock < 0) return UseCaseResult.Failure(UseCaseError.Invalid, "stock");
            if (compareAt < 0) return UseCaseResult.Failure(UseCaseError.Invalid, "compareAt");

            /*
                Every combination has to be priced, and a price of zero is not a
                price — it is a combination somebody added and never filled in,
                and publishing it gives the product away. This is the one place
                that can tell the difference, because it is the row the checkout
                charges from.

                The single exception is the one an operator asks for on purpose:
                zero against a list price above it is a hundred-percent
                discount. That is why the rule is about the pair rather than
                about `price` alone.
            */
            if (price == 0 && compareAt == 0)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "price-required");
            }

            // A "discount" that raises the price is a data error, and rendered
            // on the product page it would read as a negative saving.
            if (compareAt > 0 && compareAt <= price)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "compare-at-too-low");
            }

            skus.Add(new IncomingSku(
                code,
                Blank(incoming.Barcode),
                incoming.Combination?.Trim() ?? string.Empty,
                new Money(price),
                stock,
                incoming.Active ?? true,
                compareAt == 0 ? null : new Money(compareAt)));

            codes.Add(code);
        }

        // The unique index would catch this, but as a 500 from the database
        // rather than as the field error the form can point at.
        if (codes.Count > 0 && await repository.SkuCodeTakenAsync(codes, product.Id, cancellationToken))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "code-taken");
        }

        var existing = await repository.ListSkusAsync(product.Id, cancellationToken);

        /*
            Matched by code and updated in place, rather than the whole set
            being deleted and written again.

            A SKU's id is not an implementation detail: it is what a shopper's
            basket line names, and what an order line records to say which
            variant was sold. Re-minting it on every save meant that correcting
            one price emptied the variant out of every basket holding it — the
            line came back "unknown-sku" — and set `SkuId` to null on every
            order line that had ever referenced it, so past invoices could no
            longer say which size they were for. Neither is something a price
            edit is allowed to do, and pricing each size is exactly what the
            variants screen now asks operators to do routinely.

            The code is the identity because it is what the operator types and
            what the unique index is on. A row whose code is gone from the
            posted list is gone from the product, which is what makes a deletion
            on screen 108 a deletion here.
        */
        var byCode = existing.ToDictionary(sku => sku.Code, StringComparer.OrdinalIgnoreCase);
        var kept = new List<ProductSku>(skus.Count);

        foreach (var incoming in skus)
        {
            if (byCode.TryGetValue(incoming.Code, out var sku))
            {
                sku.Code = incoming.Code;
                sku.Barcode = incoming.Barcode;
                sku.Combination = incoming.Combination;
                sku.Price = incoming.Price;
                sku.CompareAtPrice = incoming.CompareAtPrice;
                sku.SetStock(incoming.Stock);
                sku.IsActive = incoming.IsActive;
                kept.Add(sku);
                continue;
            }

            var added = new ProductSku
            {
                ProductId = product.Id,
                Code = incoming.Code,
                Barcode = incoming.Barcode,
                Combination = incoming.Combination,
                Price = incoming.Price,
                CompareAtPrice = incoming.CompareAtPrice,
                IsActive = incoming.IsActive,
            };

            added.SetStock(incoming.Stock);
            repository.AddSku(added);
            kept.Add(added);
        }

        repository.RemoveSkus(existing.Where(sku => !kept.Contains(sku)));

        /*
            The product's own price is deliberately left alone here.

            Pricing a product and pricing its combinations are two separate
            decisions, and this used to collapse them: saving the sizes
            overwrote `product.Price` with the cheapest of them, destroying the
            figure an operator had set on the pricing screen. Removing the
            combinations afterwards then left the product priced at whatever one
            size had cost.

            What the shop window shows for a product that sells by combination
            is a question for the read side, which computes it from the
            combinations without writing anything — see
            `CatalogueQueries`. Nothing has to be kept in step because nothing
            is copied.
        */

        audit.Record("product.skus.saved", product.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// Screen 106 — replaces the product's attributes with what was posted.
    /// </summary>
    public async Task<UseCaseResult> SaveAttributesAsync(
        SaveAttributesRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseId(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        var product = await repository.FindProductAsync(id, cancellationToken);
        if (product is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        if (request.Attributes.Count > MaxRows)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "attributes");
        }

        var attributes = new List<ProductAttribute>(request.Attributes.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 0;

        foreach (var incoming in request.Attributes)
        {
            var name = incoming.Name?.Trim() ?? string.Empty;
            if (name.Length is 0 or > 100)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "name");
            }

            if (!seen.Add(name))
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "name-duplicate");
            }

            var kind = incoming.Kind?.ToLowerInvariant() switch
            {
                null or "" or "text" => AttributeKind.Text,
                "number" => AttributeKind.Number,
                "boolean" => AttributeKind.Boolean,
                _ => (AttributeKind?)null,
            };

            if (kind is null)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "kind");
            }

            var values = (incoming.Values ?? [])
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .ToList();

            var joined = string.Join(ValueSeparator, values);
            if (joined.Length > 1000)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "values");
            }

            attributes.Add(new ProductAttribute
            {
                ProductId = product.Id,
                Name = name,
                Kind = kind.Value,
                Values = joined,
                IsFilterable = incoming.Filterable ?? false,
                SortOrder = order,
            });

            order++;
        }

        var existing = await repository.ListAttributesAsync(product.Id, cancellationToken);
        repository.ReplaceAttributes(product.Id, existing, attributes);

        audit.Record("product.attributes.saved", product.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// Replaces a product's volume ladder — the B2B quantity breaks.
    /// </summary>
    /// <remarks>
    /// Under the catalogue role rather than a sales one, because this is a
    /// property of the product: what a hundred units costs to pick and ship is
    /// the same question as what one costs, and the person who owns the price
    /// owns the ladder under it.
    /// </remarks>
    public async Task<UseCaseResult> SaveVolumeTiersAsync(
        SaveProductVolumeTiersRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseId(request.Id, out var id))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "id");
        }

        var product = await repository.FindProductAsync(id, cancellationToken);
        if (product is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        if (request.Tiers.Count > MaxRows)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "tiers");
        }

        var tiers = new List<ProductVolumeTier>(request.Tiers.Count);
        var floors = new HashSet<int>();

        foreach (var incoming in request.Tiers)
        {
            // A rung starting at one unit is not a volume break, it is a price
            // change — and the product already has a field for that.
            if (incoming.MinimumQuantity is < 2 or > 1_000_000)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "minimumQuantity");
            }

            // Ninety is generous and a hundred is free, which is not a discount
            // anybody means to publish.
            if (incoming.DiscountPercent is < 1 or > 90)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "discountPercent");
            }

            if (!floors.Add(incoming.MinimumQuantity))
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "minimumQuantity-duplicate");
            }

            tiers.Add(new ProductVolumeTier
            {
                ProductId = product.Id,
                MinimumQuantity = incoming.MinimumQuantity,
                DiscountPercent = incoming.DiscountPercent,
            });
        }

        // A ladder that pays less for more is somebody having typed a row into
        // the wrong line, and it is worth refusing rather than storing: the
        // pricing would honour it, and an organisation ordering two hundred
        // would be charged more than one ordering a hundred.
        var ordered = tiers.OrderBy(tier => tier.MinimumQuantity).ToList();
        for (var index = 1; index < ordered.Count; index++)
        {
            if (ordered[index].DiscountPercent <= ordered[index - 1].DiscountPercent)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "discount-not-increasing");
            }
        }

        var existing = await repository.ListVolumeTiersAsync(product.Id, cancellationToken);
        repository.ReplaceVolumeTiers(product.Id, existing, ordered);

        audit.Record("product.volume-tiers.saved", product.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// How <see cref="ProductAttribute.Values"/> packs its list into one column.
    /// </summary>
    /// <remarks>
    /// ASCII unit separator, written as an escape so it is visible in source.
    /// A comma would not do: values are typed in Persian, where the comma is
    /// ordinary punctuation and can sit inside a single value, so splitting on
    /// one would turn that value into two on the way back out. This character
    /// cannot be typed into the form that produces these.
    /// </remarks>
    public const string ValueSeparator = "\u001F";

    /// <summary>
    /// A key SKUs and URLs can carry: lowercase, ASCII letters, digits and
    /// hyphens.
    /// </summary>
    /// <remarks>
    /// Persian labels are what an operator types; the key is what the
    /// combination string is built from and what the storefront puts in a query
    /// string, so it is derived rather than accepted as typed.
    /// </remarks>
    private static string NormaliseKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var key = new string([.. value.Trim().ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]);

        // Collapse the runs the substitution above can produce.
        while (key.Contains("--", StringComparison.Ordinal))
        {
            key = key.Replace("--", "-", StringComparison.Ordinal);
        }

        key = key.Trim('-');
        return key.Length > 50 ? key[..50] : key;
    }

    private static bool IsHexColour(string? value) =>
        value is not null
        && (value.Length is 4 or 7 or 9)
        && value[0] == '#'
        && value[1..].All(char.IsAsciiHexDigit);

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseId(string? value, out Guid id) =>
        Guid.TryParse(value, out id) && id != Guid.Empty;

    /// <summary>
    /// Whether an image URL is one this API issued into <paramref name="folder"/>.
    /// </summary>
    /// <remarks>
    /// An empty string is how a form clears an image, and is allowed through so
    /// that removing a logo stays possible.
    /// </remarks>
    private bool IsStorableImage(string url, string folder) =>
        url.Length == 0 || storage.IsOwnUrl(url, folder);
}
