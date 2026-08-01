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
    /// <summary>The only folder a product image may come from.</summary>
    private const string ProductImageFolder = "products";

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
            // Every URL has to be one this API issued into the product folder.
            // These are rendered on the storefront to every visitor, so a field
            // that took any URL would let whoever can edit a product point the
            // catalogue at an off-site host.
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

        if (request.Price is { } price)
        {
            if (price < 0) return UseCaseResult.Failure(UseCaseError.Invalid, "price");
            product.Price = new Money(price);
        }

        if (request.CostPrice is { } cost)
        {
            if (cost < 0) return UseCaseResult.Failure(UseCaseError.Invalid, "costPrice");
            product.CostPrice = new Money(cost);
        }

        if (request.CompareAtPrice is { } compareAt)
        {
            // A compare-at below the selling price would render as a negative
            // saving on the product card.
            if (compareAt != 0 && compareAt < product.Price.Amount)
            {
                return UseCaseResult.Failure(UseCaseError.Invalid, "compareAtPrice");
            }

            product.CompareAtPrice = compareAt == 0 ? null : new Money(compareAt);
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
        if (request.Description is not null) brand.Description = request.Description;
        if (request.Logo is not null) brand.LogoUrl = request.Logo;

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
        if (request.Description is not null) collection.Summary = request.Description;
        if (request.Cover is not null) collection.CoverUrl = request.Cover;

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
        if (request.Cover is not null) entry.CoverUrl = request.Cover;

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

        if (request.Code is not null) coupon.Code = request.Code.Trim().ToUpperInvariant();
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
    public async Task<UseCaseResult> RecordStockMovementAsync(
        Guid actorId,
        StockMovementRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseId(request.ProductId, out var productId))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "productId");
        }

        if (!Enum.TryParse<StockMovementKind>(request.Kind, ignoreCase: true, out var kind))
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "kind");
        }

        if (request.Quantity < 0)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "quantity");
        }

        var product = await repository.FindProductAsync(productId, cancellationToken);
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

        audit.Record($"inventory.{request.Kind.ToLowerInvariant()}", product.Sku is { Length: > 0 } sku ? sku : product.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
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

    private static bool TryParseId(string? value, out Guid id) =>
        Guid.TryParse(value, out id) && id != Guid.Empty;
}
