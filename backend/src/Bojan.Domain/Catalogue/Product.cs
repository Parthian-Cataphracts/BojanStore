using Bojan.Domain.Common;

namespace Bojan.Domain.Catalogue;

/// <summary>
/// A sellable product.
/// </summary>
/// <remarks>
/// Mirrors the frontend's <c>Product</c> DTO field for field — see
/// <c>apps/storefront/src/lib/api/types.ts</c>. This is the DTO to get exactly
/// right: it appears in nine different storefront screens (per
/// <c>BACKEND.md</c>, Phase 2).
///
/// <see cref="CostPrice"/> exists only for the panel's pricing screen
/// (<c>apps/admin/src/lib/api/resources.ts</c>, the <c>product-pricing</c>
/// resource) and must never be serialised into a storefront-facing
/// <c>Product</c> response — <c>BACKEND.md</c> Phase 7 says so explicitly.
/// </remarks>
public sealed class Product : SoftDeletableEntity
{
    public required string Slug { get; set; }

    public required string Title { get; set; }

    public required Guid BrandId { get; set; }

    public required Guid CategoryId { get; set; }

    /// <summary>Selling price, in Toman.</summary>
    public required Money Price { get; set; }

    /// <summary>List price before discount, when the product is on sale.</summary>
    public Money? CompareAtPrice { get; set; }

    /// <summary>
    /// What the store paid — owner/product role only, storefront never sees this.
    /// </summary>
    /// <remarks>
    /// Required rather than optional, defaulting to zero: the panel's product
    /// list renders it as a number either way, and "not recorded" and "cost
    /// nothing" are the same figure in every margin the reports compute. Being
    /// required is also what lets it be a mapped column the stock-value and
    /// gross-profit aggregates can sum in SQL.
    /// </remarks>
    public Money CostPrice { get; set; } = Money.Zero;

    public int Stock { get; set; }

    /// <summary>
    /// At or below this, the panel calls the product low on stock.
    /// </summary>
    /// <remarks>
    /// Per product rather than one number for the shop: a pen ordered by the
    /// gross and a desk lamp ordered singly do not run low at the same count.
    /// The inventory screens and the dashboard's "low stock" tile both read
    /// this, so raising it for one product changes what those show for that
    /// product alone.
    /// </remarks>
    public int LowStockThreshold { get; set; } = 5;

    /// <summary>
    /// Whether stock is counted for this product at all.
    /// </summary>
    /// <remarks>
    /// Off for anything not held in a warehouse — a made-to-order item, a gift
    /// card. Order placement skips the availability check and the reservation
    /// when this is off, which is the only thing that makes it more than a
    /// label: leaving it on the form while the checkout still refused an
    /// untracked product would be a switch that reads as doing something.
    /// </remarks>
    public bool TrackStock { get; set; } = true;

    /// <summary>Whether the product may be ordered past its stock, to be fulfilled later.</summary>
    public bool AllowBackorder { get; set; }

    /// <summary>Overrides the product title in the storefront's <c>&lt;title&gt;</c> when set.</summary>
    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    /// <summary>Whether stock has to be on hand before this product can be bought.</summary>
    public bool RequiresStockOnHand => TrackStock && !AllowBackorder;

    public required string ImageUrl { get; set; }

    public string ImageAlt { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsNew { get; set; }

    public bool IsBestseller { get; set; }

    public bool IsPublished { get; set; } = true;

    public string Sku { get; set; } = string.Empty;

    private readonly List<ProductImage> _gallery = [];
    public IReadOnlyCollection<ProductImage> Gallery => _gallery;

    private readonly List<ProductSpec> _specs = [];
    public IReadOnlyCollection<ProductSpec> Specs => _specs;

    /// <summary>
    /// Reduces stock for a sale. Throws rather than going negative — the
    /// caller (order placement) must re-check availability inside its own
    /// transaction regardless; see <c>BACKEND.md</c> Phase 4, rule 2.
    /// </summary>
    public void ReduceStock(int quantity)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity cannot be negative.");
        }

        if (!TrackStock)
        {
            // Nothing is being counted, so there is nothing to take away.
            return;
        }

        if (quantity > Stock)
        {
            if (!AllowBackorder)
            {
                throw new InvalidOperationException(
                    $"Cannot reduce stock of '{Slug}' by {quantity}; only {Stock} available.");
            }

            // A backorder is allowed to run the count negative — that is what
            // records how much is owed once a delivery arrives.
            Stock -= quantity;
            return;
        }

        Stock -= quantity;
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity cannot be negative.");
        }

        Stock += quantity;
    }

    public void AddGalleryImage(string url, int sortOrder) => _gallery.Add(new ProductImage
    {
        ProductId = Id,
        Url = url,
        SortOrder = sortOrder,
    });

    /// <summary>
    /// Replaces the whole gallery with <paramref name="urls"/>, in order.
    /// </summary>
    /// <remarks>
    /// The images screen posts the gallery as one list, because reordering and
    /// removing are the two things it does and neither is expressible as a
    /// sequence of adds. Replacing wholesale is also what makes a removal
    /// actually remove: appending to what is already there would leave every
    /// deleted image in place.
    /// </remarks>
    public void ReplaceGallery(IEnumerable<string> urls)
    {
        _gallery.Clear();

        var order = 0;
        foreach (var url in urls)
        {
            _gallery.Add(new ProductImage { ProductId = Id, Url = url, SortOrder = order });
            order++;
        }
    }

    public void AddSpec(string label, string value) => _specs.Add(new ProductSpec
    {
        ProductId = Id,
        Label = label,
        Value = value,
    });
}

/// <summary>One entry in a product's photo gallery, beyond its primary <see cref="Product.ImageUrl"/>.</summary>
public sealed class ProductImage : Entity
{
    public required Guid ProductId { get; init; }

    public required string Url { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>A label/value spec row — screen 6's attributes table.</summary>
public sealed class ProductSpec : Entity
{
    public required Guid ProductId { get; init; }

    public required string Label { get; set; }

    public required string Value { get; set; }
}
