using Bojan.Application.Auth;
using Bojan.Application.Common;
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
    IUnitOfWork unitOfWork,
    IAuditLog audit,
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

        if (TryParseId(request.Id, out var id))
        {
            var existing = await repository.FindProductWithDetailAsync(id, cancellationToken);
            if (existing is null)
            {
                return UseCaseResult<string>.Failure(UseCaseError.NotFound);
            }

            product = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "title");
            }

            var brand = await ResolveBrandAsync(request.Brand, cancellationToken);
            var category = await ResolveCategoryAsync(request.Category, cancellationToken);

            if (brand is null || category is null)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, brand is null ? "brand" : "category");
            }

            var desired = Slug.From(request.Title);
            var slug = await UniqueProductSlugAsync(desired, null, cancellationToken);

            product = new Product
            {
                Slug = slug,
                Title = request.Title,
                BrandId = brand.Id,
                CategoryId = category.Id,
                Price = new Money(request.Price ?? 0),
                ImageUrl = request.Images?.FirstOrDefault() ?? string.Empty,
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

        if (request.Category is not null)
        {
            var category = await ResolveCategoryAsync(request.Category, cancellationToken);
            if (category is null) return UseCaseResult<string>.Failure(UseCaseError.Invalid, "category");
            product.CategoryId = category.Id;
        }

        if (request.Status is { } status)
        {
            // "archived" is the soft delete, not a third boolean — see
            // WireFormat.ProductStatus for why the state is derived.
            switch (status)
            {
                case "archived":
                    product.SoftDelete(clock.UtcNow);
                    break;
                case "published":
                    product.Restore();
                    product.IsPublished = true;
                    break;
                case "draft":
                    product.Restore();
                    product.IsPublished = false;
                    break;
                default:
                    return UseCaseResult<string>.Failure(UseCaseError.Invalid, "status");
            }
        }

        if (request.Images is { Count: > 0 } images)
        {
            // Every URL has to be one this API issued into the product folder —
            // see the folder constants for why this is not a formality.
            if (images.Any(url => !storage.IsOwnUrl(url, ProductImageFolder)))
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

        audit.Record("product.saved", product.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return product.Id.ToString();
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
            if (!IsStorableImage(request.Logo, BrandImageFolder))
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
            if (!IsStorableImage(request.Cover, CollectionImageFolder))
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
            if (!IsStorableImage(request.Cover, ContentImageFolder))
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
            if (!Enum.TryParse<CampaignStatus>(request.Status, ignoreCase: true, out var status))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "status");
            }

            campaign.Status = status;
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

    private async Task<Category?> ResolveCategoryAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return TryParseId(value, out var id)
            ? await repository.FindCategoryAsync(id, cancellationToken)
            : await repository.FindCategoryBySlugAsync(value, cancellationToken);
    }

    private async Task<string> UniqueProductSlugAsync(string desired, Guid? exceptId, CancellationToken cancellationToken)
    {
        var candidate = desired;
        for (var suffix = 2; suffix < 1000; suffix++)
        {
            if (!await repository.ProductSlugExistsAsync(candidate, exceptId, cancellationToken))
            {
                return candidate;
            }

            candidate = $"{desired}-{suffix}";
        }

        throw new InvalidOperationException($"Could not find a free product slug based on '{desired}'.");
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

        var skus = new List<ProductSku>(request.Skus.Count);
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

            if (price < 0) return UseCaseResult.Failure(UseCaseError.Invalid, "price");
            if (stock < 0) return UseCaseResult.Failure(UseCaseError.Invalid, "stock");

            var sku = new ProductSku
            {
                ProductId = product.Id,
                Code = code,
                Barcode = Blank(incoming.Barcode),
                Combination = incoming.Combination?.Trim() ?? string.Empty,
                Price = new Money(price),
                IsActive = incoming.Active ?? true,
            };

            sku.SetStock(stock);

            skus.Add(sku);
            codes.Add(code);
        }

        // The unique index would catch this, but as a 500 from the database
        // rather than as the field error the form can point at.
        if (codes.Count > 0 && await repository.SkuCodeTakenAsync(codes, product.Id, cancellationToken))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "code-taken");
        }

        var existing = await repository.ListSkusAsync(product.Id, cancellationToken);
        repository.ReplaceSkus(product.Id, existing, skus);

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
