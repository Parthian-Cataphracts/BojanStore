using Bojan.Application.Administration;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Business;
using Bojan.Domain.Orders;
using Bojan.Domain.Support;
using Bojan.Infrastructure.Persistence;
using Bojan.Infrastructure.Persistence.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Queries;

/// <summary>
/// Phase 6's reads — the panel's lists, its dashboard and its reports.
/// </summary>
/// <remarks>
/// <para>
/// Every aggregate is a <c>GroupBy</c> EF translates, never a
/// <c>ToListAsync</c> followed by LINQ-to-objects over orders. That is
/// <c>BACKEND.md</c> Phase 6's instruction, and the difference is between a
/// dashboard that costs one grouped scan and one that costs the shop's entire
/// order history transferred to the API.
/// </para>
/// <para>
/// Time series group by year/month/day components rather than by a truncated
/// timestamp, because <c>date_trunc</c> has no portable equivalent and the API
/// tests run on SQLite. A weekly series is rolled up from the daily grouping —
/// at most 366 already-aggregated rows, not the orders behind them.
/// </para>
/// </remarks>
public sealed class AdminQueries(BojanDbContext db) : IAdminQueries
{
    /// <summary>Below this a product shows as "low stock" on screen 107.</summary>
    private const int LowStockThreshold = 5;

    // -- lists ---------------------------------------------------------------

    public async Task<Paged<AdminOrderDto>> ListOrdersAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var orders = db.Orders.AsNoTracking();

        if (WireFormat.ParseOrderStatus(normalised.Status) is { } status)
        {
            orders = orders.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            orders = orders.Where(o =>
                o.Number.Contains(needle)
                || db.Customers.Any(c => c.Id == o.CustomerId
                    && (c.Phone.Contains(needle) || (c.FirstName + " " + c.LastName).Contains(needle))));
        }

        if (normalised.From is { } from) orders = orders.Where(o => o.PlacedAtUtc >= from);
        if (normalised.To is { } to) orders = orders.Where(o => o.PlacedAtUtc <= to);

        var total = await orders.CountAsync(cancellationToken);

        var rows = await orders
            .OrderByDescending(o => o.PlacedAtUtc)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(o => new OrderRow(
                o.Id,
                o.Number,
                db.Customers.Where(c => c.Id == o.CustomerId).Select(c => c.FirstName + " " + c.LastName).FirstOrDefault(),
                db.Customers.Where(c => c.Id == o.CustomerId).Select(c => c.Phone).FirstOrDefault(),
                o.PlacedAtUtc,
                o.Status,
                o.Lines.Count,
                o.Subtotal,
                o.Discount,
                o.Shipping,
                o.PaymentMethodName,
                o.ShippingMethodName,
                o.ShippingAddressSnapshot))
            .ToListAsync(cancellationToken);

        return new Paged<AdminOrderDto>([.. rows.Select(r => ToDto(r, []))], total, normalised.Page, normalised.PageSize);
    }

    private sealed record OrderRow(
        Guid Id, string Number, string? Customer, string? Phone, DateTimeOffset PlacedAt, OrderStatus Status,
        int ItemCount, Domain.Common.Money Subtotal, Domain.Common.Money Discount, Domain.Common.Money Shipping,
        string PaymentMethod, string ShippingMethod, string Address);

    private static AdminOrderDto ToDto(OrderRow row, IReadOnlyList<AdminOrderItemDto> items) => new(
        row.Id.ToString(),
        row.Number,
        (row.Customer ?? string.Empty).Trim(),
        row.Phone ?? string.Empty,
        row.PlacedAt,
        WireFormat.AdminOrderStatus(row.Status),
        row.ItemCount,
        (row.Subtotal.ClampedMinus(row.Discount) + row.Shipping).Amount,
        row.PaymentMethod,
        row.ShippingMethod,
        row.Address,
        items);

    public async Task<AdminOrderDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var row = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new OrderRow(
                o.Id,
                o.Number,
                db.Customers.Where(c => c.Id == o.CustomerId).Select(c => c.FirstName + " " + c.LastName).FirstOrDefault(),
                db.Customers.Where(c => c.Id == o.CustomerId).Select(c => c.Phone).FirstOrDefault(),
                o.PlacedAtUtc,
                o.Status,
                o.Lines.Count,
                o.Subtotal,
                o.Discount,
                o.Shipping,
                o.PaymentMethodName,
                o.ShippingMethodName,
                o.ShippingAddressSnapshot))
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var items = await db.OrderLines.AsNoTracking()
            .Where(l => l.OrderId == orderId)
            .Select(l => new
            {
                l.ProductTitle,
                Sku = db.Products.Where(p => p.Id == l.ProductId).Select(p => p.Sku).FirstOrDefault(),
                l.Quantity,
                l.UnitPrice,
            })
            .ToListAsync(cancellationToken);

        return ToDto(row, [.. items.Select(i => new AdminOrderItemDto(
            i.ProductTitle, i.Sku ?? string.Empty, i.Quantity, i.UnitPrice.Amount))]);
    }

    public async Task<Paged<AdminProductDto>> ListProductsAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();

        // The panel lists archived products too — "archived" is one of the
        // status filter's own values, so the global soft-delete filter is off
        // here and the status is derived instead.
        var products = db.Products.AsNoTracking().IgnoreQueryFilters();

        products = normalised.Status switch
        {
            "published" => products.Where(p => p.IsPublished && p.DeletedAtUtc == null),
            "draft" => products.Where(p => !p.IsPublished && p.DeletedAtUtc == null),
            "archived" => products.Where(p => p.DeletedAtUtc != null),
            _ => products,
        };

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            products = products.Where(p => p.Title.Contains(needle) || p.Sku.Contains(needle));
        }

        var total = await products.CountAsync(cancellationToken);

        var rows = await products
            .OrderBy(p => p.Title)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Sku,
                p.Title,
                Brand = db.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                BrandSlug = db.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Slug).FirstOrDefault(),
                Category = db.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault(),
                CategorySlug = db.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Slug).FirstOrDefault(),
                p.Price,
                p.CostPrice,
                p.Stock,
                p.IsPublished,
                p.DeletedAtUtc,
                p.ImageUrl,
            })
            .ToListAsync(cancellationToken);

        return new Paged<AdminProductDto>(
            [.. rows.Select(p => new AdminProductDto(
                p.Id.ToString(),
                p.Sku,
                p.Title,
                p.Brand ?? string.Empty,
                p.BrandSlug ?? string.Empty,
                p.Category ?? string.Empty,
                p.CategorySlug ?? string.Empty,
                p.Price.Amount,
                p.CostPrice.Amount,
                p.Stock,
                WireFormat.ProductStatus(p.IsPublished, p.DeletedAtUtc != null),
                p.ImageUrl,
                p.DeletedAtUtc ?? DateTimeOffset.UtcNow))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<AdminProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        var row = await db.Products.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.Id == productId)
            .Select(p => new
            {
                p.Id,
                p.Sku,
                p.Title,
                Brand = db.Brands.IgnoreQueryFilters().Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                BrandSlug = db.Brands.IgnoreQueryFilters().Where(b => b.Id == p.BrandId).Select(b => b.Slug).FirstOrDefault(),
                Category = db.Categories.IgnoreQueryFilters().Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault(),
                CategorySlug = db.Categories.IgnoreQueryFilters().Where(c => c.Id == p.CategoryId).Select(c => c.Slug).FirstOrDefault(),
                p.Price,
                p.CostPrice,
                p.Stock,
                p.IsPublished,
                p.DeletedAtUtc,
                p.ImageUrl,
                // Primary first, then the gallery in its stored order — the
                // order screen 105 shows and posts back.
                Gallery = p.Gallery.OrderBy(image => image.SortOrder).Select(image => image.Url).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new AdminProductDto(
                row.Id.ToString(),
                row.Sku,
                row.Title,
                row.Brand ?? string.Empty,
                row.BrandSlug ?? string.Empty,
                row.Category ?? string.Empty,
                row.CategorySlug ?? string.Empty,
                row.Price.Amount,
                row.CostPrice.Amount,
                row.Stock,
                WireFormat.ProductStatus(row.IsPublished, row.DeletedAtUtc != null),
                row.ImageUrl,
                row.DeletedAtUtc ?? DateTimeOffset.UtcNow,
                [row.ImageUrl, .. row.Gallery]);
    }

    public async Task<Paged<AdminCategoryDto>> ListCategoriesAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var categories = db.Categories.AsNoTracking().IgnoreQueryFilters();

        categories = normalised.Status switch
        {
            "published" => categories.Where(c => c.IsPublished && c.DeletedAtUtc == null),
            "draft" => categories.Where(c => !c.IsPublished && c.DeletedAtUtc == null),
            "archived" => categories.Where(c => c.DeletedAtUtc != null),
            _ => categories,
        };

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            categories = categories.Where(c => c.Name.Contains(needle) || c.Slug.Contains(needle));
        }

        var total = await categories.CountAsync(cancellationToken);

        var rows = await categories
            .OrderBy(c => c.Name)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Slug,
                c.Icon,
                c.ImageUrl,
                c.ParentId,
                ParentTitle = db.Categories.Where(p => p.Id == c.ParentId).Select(p => p.Name).FirstOrDefault(),
                ProductCount = db.Products.Count(p => p.CategoryId == c.Id && p.DeletedAtUtc == null),
                c.IsPublished,
                c.DeletedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return new Paged<AdminCategoryDto>(
            [.. rows.Select(c => new AdminCategoryDto(
                c.Id.ToString(),
                c.Name,
                c.Slug,
                c.Icon,
                c.ImageUrl,
                c.ParentId?.ToString(),
                c.ParentTitle,
                c.ProductCount,
                WireFormat.ProductStatus(c.IsPublished, c.DeletedAtUtc != null)))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<AdminCategoryDto?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var row = await db.Categories.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.Id == categoryId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Slug,
                c.Icon,
                c.ImageUrl,
                c.ParentId,
                ParentTitle = db.Categories.Where(p => p.Id == c.ParentId).Select(p => p.Name).FirstOrDefault(),
                ProductCount = db.Products.Count(p => p.CategoryId == c.Id && p.DeletedAtUtc == null),
                c.IsPublished,
                c.DeletedAtUtc,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new AdminCategoryDto(
                row.Id.ToString(),
                row.Name,
                row.Slug,
                row.Icon,
                row.ImageUrl,
                row.ParentId?.ToString(),
                row.ParentTitle,
                row.ProductCount,
                WireFormat.ProductStatus(row.IsPublished, row.DeletedAtUtc != null));
    }

    public async Task<Paged<AdminBrandDto>> ListBrandsAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var brands = db.Brands.AsNoTracking().IgnoreQueryFilters();

        brands = normalised.Status switch
        {
            "published" => brands.Where(b => b.IsPublished && b.DeletedAtUtc == null),
            "draft" => brands.Where(b => !b.IsPublished && b.DeletedAtUtc == null),
            "archived" => brands.Where(b => b.DeletedAtUtc != null),
            _ => brands,
        };

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            brands = brands.Where(b => b.Name.Contains(needle) || b.Slug.Contains(needle));
        }

        var total = await brands.CountAsync(cancellationToken);

        var rows = await brands
            .OrderBy(b => b.Name)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.Slug,
                b.Tagline,
                b.Description,
                b.LogoUrl,
                b.CoverUrl,
                b.IsFeatured,
                ProductCount = db.Products.Count(p => p.BrandId == b.Id && p.DeletedAtUtc == null),
                b.IsPublished,
                b.DeletedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return new Paged<AdminBrandDto>(
            [.. rows.Select(b => new AdminBrandDto(
                b.Id.ToString(),
                b.Name,
                b.Slug,
                b.Tagline,
                b.Description,
                b.LogoUrl,
                b.CoverUrl,
                b.IsFeatured,
                b.ProductCount,
                WireFormat.ProductStatus(b.IsPublished, b.DeletedAtUtc != null)))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<AdminBrandDto?> GetBrandAsync(Guid brandId, CancellationToken cancellationToken)
    {
        var row = await db.Brands.AsNoTracking().IgnoreQueryFilters()
            .Where(b => b.Id == brandId)
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.Slug,
                b.Tagline,
                b.Description,
                b.LogoUrl,
                b.CoverUrl,
                b.IsFeatured,
                ProductCount = db.Products.Count(p => p.BrandId == b.Id && p.DeletedAtUtc == null),
                b.IsPublished,
                b.DeletedAtUtc,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new AdminBrandDto(
                row.Id.ToString(),
                row.Name,
                row.Slug,
                row.Tagline,
                row.Description,
                row.LogoUrl,
                row.CoverUrl,
                row.IsFeatured,
                row.ProductCount,
                WireFormat.ProductStatus(row.IsPublished, row.DeletedAtUtc != null));
    }

    public async Task<Paged<AdminCollectionDto>> ListCollectionsAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var collections = db.Collections.AsNoTracking().IgnoreQueryFilters();

        collections = normalised.Status switch
        {
            "published" => collections.Where(c => c.IsPublished && c.DeletedAtUtc == null),
            "draft" => collections.Where(c => !c.IsPublished && c.DeletedAtUtc == null),
            "archived" => collections.Where(c => c.DeletedAtUtc != null),
            _ => collections,
        };

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            collections = collections.Where(c => c.Title.Contains(needle) || c.Slug.Contains(needle));
        }

        var total = await collections.CountAsync(cancellationToken);

        var rows = await collections
            .OrderBy(c => c.Title)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Slug,
                c.Summary,
                c.CoverUrl,
                c.EditorialNote,
                c.IsFeatured,
                ProductCount = c.Products.Count,
                c.IsPublished,
                c.DeletedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return new Paged<AdminCollectionDto>(
            [.. rows.Select(c => new AdminCollectionDto(
                c.Id.ToString(),
                c.Title,
                c.Slug,
                c.Summary,
                c.CoverUrl,
                c.EditorialNote,
                c.IsFeatured,
                c.ProductCount,
                WireFormat.ProductStatus(c.IsPublished, c.DeletedAtUtc != null)))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<AdminCollectionDto?> GetCollectionAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        var row = await db.Collections.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.Id == collectionId)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Slug,
                c.Summary,
                c.CoverUrl,
                c.EditorialNote,
                c.IsFeatured,
                ProductCount = c.Products.Count,
                c.IsPublished,
                c.DeletedAtUtc,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new AdminCollectionDto(
                row.Id.ToString(),
                row.Title,
                row.Slug,
                row.Summary,
                row.CoverUrl,
                row.EditorialNote,
                row.IsFeatured,
                row.ProductCount,
                WireFormat.ProductStatus(row.IsPublished, row.DeletedAtUtc != null));
    }

    public async Task<Paged<AdminCustomerDto>> ListCustomersAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var customers = db.Customers.AsNoTracking();

        customers = normalised.Status switch
        {
            "active" => customers.Where(c => !c.IsBlocked),
            "blocked" => customers.Where(c => c.IsBlocked),
            _ => customers,
        };

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            customers = customers.Where(c =>
                c.Phone.Contains(needle)
                || (c.FirstName + " " + c.LastName).Contains(needle)
                || (c.Email != null && c.Email.Contains(needle)));
        }

        var total = await customers.CountAsync(cancellationToken);

        // Order count and lifetime spend are correlated aggregates, not a join
        // with a group-by: one row per customer either way, and this shape
        // survives the paging above it without a second query.
        var rows = await customers
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(c => new
            {
                c.Id,
                c.FirstName,
                c.LastName,
                c.Phone,
                c.Email,
                c.Group,
                c.CreatedAtUtc,
                c.IsBlocked,
                OrderCount = db.Orders.Count(o => o.CustomerId == c.Id && o.Status != OrderStatus.Cancelled),
                TotalSpent = db.Orders
                    .Where(o => o.CustomerId == c.Id && o.Status != OrderStatus.Cancelled)
                    .Sum(o => (long?)o.Subtotal.Amount - (long?)o.Discount.Amount + (long?)o.Shipping.Amount) ?? 0L,
            })
            .ToListAsync(cancellationToken);

        return new Paged<AdminCustomerDto>(
            [.. rows.Select(c => new AdminCustomerDto(
                c.Id.ToString(),
                $"{c.FirstName} {c.LastName}".Trim(),
                c.Phone,
                c.Email,
                c.Group,
                c.OrderCount,
                c.TotalSpent,
                c.CreatedAtUtc,
                WireFormat.CustomerStatus(c.IsBlocked)))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<AdminCustomerDto?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var page = await ListCustomersAsync(new AdminListQuery(PageSize: AdminListQuery.MaxPageSize), cancellationToken);
        return page.Items.FirstOrDefault(c => c.Id == customerId.ToString());
    }

    public async Task<Paged<InventoryRowDto>> ListInventoryAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var products = db.Products.AsNoTracking();

        products = normalised.Status switch
        {
            "out" => products.Where(p => p.Stock == 0),
            "low" => products.Where(p => p.Stock > 0 && p.Stock <= LowStockThreshold),
            "in" => products.Where(p => p.Stock > LowStockThreshold),
            _ => products,
        };

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            products = products.Where(p => p.Title.Contains(needle) || p.Sku.Contains(needle));
        }

        var total = await products.CountAsync(cancellationToken);

        var rows = await products
            .OrderBy(p => p.Stock)
            .ThenBy(p => p.Title)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(p => new InventoryRowDto(
                p.Id.ToString(),
                p.Sku,
                p.Title,
                db.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                p.Stock,
                LowStockThreshold,
                db.StockMovements.Where(m => m.ProductId == p.Id)
                    .OrderByDescending(m => m.AtUtc)
                    .Select(m => (DateTimeOffset?)m.AtUtc)
                    .FirstOrDefault() ?? DateTimeOffset.UnixEpoch))
            .ToListAsync(cancellationToken);

        return new Paged<InventoryRowDto>(rows, total, normalised.Page, normalised.PageSize);
    }

    public async Task<Paged<StockMovementDto>> ListStockMovementsAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var movements = db.StockMovements.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalised.Kind))
        {
            if (!Enum.TryParse<Domain.Inventory.StockMovementKind>(normalised.Kind, ignoreCase: true, out var kind))
            {
                return new Paged<StockMovementDto>([], 0, normalised.Page, normalised.PageSize);
            }

            movements = movements.Where(m => m.Kind == kind);
        }

        if (normalised.From is { } from) movements = movements.Where(m => m.AtUtc >= from);
        if (normalised.To is { } to) movements = movements.Where(m => m.AtUtc <= to);

        var total = await movements.CountAsync(cancellationToken);

        var rows = await movements
            .OrderByDescending(m => m.AtUtc)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(m => new
            {
                m.Id,
                Sku = db.Products.IgnoreQueryFilters().Where(p => p.Id == m.ProductId).Select(p => p.Sku).FirstOrDefault(),
                Title = db.Products.IgnoreQueryFilters().Where(p => p.Id == m.ProductId).Select(p => p.Title).FirstOrDefault(),
                m.Kind,
                m.Quantity,
                m.Reason,
                m.AtUtc,
                By = db.AdminUsers.Where(a => a.Id == m.ActorId).Select(a => a.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return new Paged<StockMovementDto>(
            [.. rows.Select(m => new StockMovementDto(
                m.Id.ToString(),
                m.Sku ?? string.Empty,
                m.Title ?? string.Empty,
                WireFormat.StockMovementKind(m.Kind),
                m.Quantity,
                m.Reason,
                m.AtUtc,
                m.By ?? string.Empty))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<Paged<AdminBusinessRequestDto>> ListBusinessRequestsAsync(
        AdminListQuery query,
        CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var requests = db.BusinessRequests.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalised.Status))
        {
            if (!Enum.TryParse<BusinessRequestStatus>(normalised.Status, ignoreCase: true, out var status))
            {
                return new Paged<AdminBusinessRequestDto>([], 0, normalised.Page, normalised.PageSize);
            }

            requests = requests.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            requests = requests.Where(r =>
                r.Code.Contains(needle) || r.Organization.Contains(needle) || r.ContactName.Contains(needle));
        }

        var total = await requests.CountAsync(cancellationToken);

        var rows = await requests
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(r => new
            {
                r.Id, r.Code, r.Title, r.Kind, r.Status, r.Organization, r.ContactName,
                r.Phone, r.Email, r.ItemCount, r.AssigneeId, r.InternalNote, r.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return new Paged<AdminBusinessRequestDto>(
            [.. rows.Select(r => new AdminBusinessRequestDto(
                r.Id.ToString(),
                r.Code,
                r.Title,
                WireFormat.BusinessRequestKind(r.Kind),
                WireFormat.BusinessRequestStatus(r.Status),
                r.Organization,
                r.ContactName,
                r.Phone,
                r.Email,
                r.ItemCount,
                r.AssigneeId?.ToString(),
                r.InternalNote,
                r.CreatedAtUtc))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<Paged<AdminCouponDto>> ListCouponsAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var coupons = db.Coupons.AsNoTracking();

        coupons = normalised.Status switch
        {
            "active" => coupons.Where(c => c.IsActive),
            "inactive" => coupons.Where(c => !c.IsActive),
            _ => coupons,
        };

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim().ToUpperInvariant();
            coupons = coupons.Where(c => c.Code.Contains(needle));
        }

        var total = await coupons.CountAsync(cancellationToken);

        var rows = await coupons
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.Code)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(c => new
            {
                c.Id, c.Code, c.PercentOff, c.AmountOff, c.MaxRedemptions,
                c.RedemptionCount, c.ExpiresAtUtc, c.IsActive,
            })
            .ToListAsync(cancellationToken);

        return new Paged<AdminCouponDto>(
            [.. rows.Select(c => new AdminCouponDto(
                c.Id.ToString(),
                c.Code,
                c.Code,
                c.PercentOff,
                c.AmountOff?.Amount,
                c.MaxRedemptions ?? 0,
                c.RedemptionCount,
                c.ExpiresAtUtc,
                c.IsActive,
                null))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<AdminCouponDto?> GetCouponAsync(Guid couponId, CancellationToken cancellationToken)
    {
        var row = await db.Coupons.AsNoTracking()
            .Where(c => c.Id == couponId)
            .Select(c => new
            {
                c.Id, c.Code, c.PercentOff, c.AmountOff, c.MinimumSpend, c.MaxRedemptions,
                c.RedemptionCount, c.ExpiresAtUtc, c.IsActive,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new AdminCouponDto(
                row.Id.ToString(),
                row.Code,
                row.Code,
                row.PercentOff,
                row.AmountOff?.Amount,
                row.MaxRedemptions ?? 0,
                row.RedemptionCount,
                row.ExpiresAtUtc,
                row.IsActive,
                row.MinimumSpend?.Amount);
    }

    public async Task<Paged<CampaignDto>> ListCampaignsAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var campaigns = db.Campaigns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalised.Status))
        {
            if (!Enum.TryParse<Domain.Marketing.CampaignStatus>(normalised.Status, ignoreCase: true, out var status))
            {
                return new Paged<CampaignDto>([], 0, normalised.Page, normalised.PageSize);
            }

            campaigns = campaigns.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            campaigns = campaigns.Where(c => c.Title.Contains(needle));
        }

        var total = await campaigns.CountAsync(cancellationToken);

        var rows = await campaigns
            .OrderByDescending(c => c.StartsAtUtc)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(c => new { c.Id, c.Title, c.Kind, c.Status, c.StartsAtUtc, c.EndsAtUtc, c.Reach, c.Conversion })
            .ToListAsync(cancellationToken);

        return new Paged<CampaignDto>(
            [.. rows.Select(c => new CampaignDto(
                c.Id.ToString(),
                c.Title,
                WireFormat.CampaignKind(c.Kind),
                WireFormat.CampaignStatus(c.Status),
                c.StartsAtUtc,
                c.EndsAtUtc,
                c.Reach,
                c.Conversion))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<CampaignDto?> GetCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var row = await db.Campaigns.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.Id == campaignId)
            .Select(c => new
            {
                c.Id, c.Title, c.Kind, c.Status, c.StartsAtUtc, c.EndsAtUtc, c.Reach, c.Conversion, c.Description,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new CampaignDto(
                row.Id.ToString(),
                row.Title,
                WireFormat.CampaignKind(row.Kind),
                WireFormat.CampaignStatus(row.Status),
                row.StartsAtUtc,
                row.EndsAtUtc,
                row.Reach,
                row.Conversion,
                row.Description);
    }

    public async Task<Paged<ContentEntryDto>> ListContentAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var entries = db.ContentEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalised.Kind))
        {
            if (!Enum.TryParse<Domain.Content.ContentKind>(normalised.Kind, ignoreCase: true, out var kind))
            {
                return new Paged<ContentEntryDto>([], 0, normalised.Page, normalised.PageSize);
            }

            entries = entries.Where(e => e.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(normalised.Status))
        {
            if (!Enum.TryParse<Domain.Content.ContentStatus>(normalised.Status, ignoreCase: true, out var status))
            {
                return new Paged<ContentEntryDto>([], 0, normalised.Page, normalised.PageSize);
            }

            entries = entries.Where(e => e.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            entries = entries.Where(e => e.Title.Contains(needle));
        }

        var total = await entries.CountAsync(cancellationToken);

        var rows = await entries
            .OrderByDescending(e => e.UpdatedAtUtc)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(e => new { e.Id, e.Title, e.Kind, e.Status, e.Author, e.UpdatedAtUtc })
            .ToListAsync(cancellationToken);

        return new Paged<ContentEntryDto>(
            [.. rows.Select(e => new ContentEntryDto(
                e.Id.ToString(),
                e.Title,
                WireFormat.ContentKind(e.Kind),
                WireFormat.ContentStatus(e.Status),
                e.Author,
                e.UpdatedAtUtc))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<ContentEntryDto?> GetContentAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var row = await db.ContentEntries.AsNoTracking().IgnoreQueryFilters()
            .Where(e => e.Id == contentId)
            .Select(e => new
            {
                e.Id, e.Title, e.Kind, e.Status, e.Author, e.UpdatedAtUtc, e.Slug, e.Excerpt, e.Body, e.CoverUrl,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new ContentEntryDto(
                row.Id.ToString(),
                row.Title,
                WireFormat.ContentKind(row.Kind),
                WireFormat.ContentStatus(row.Status),
                row.Author,
                row.UpdatedAtUtc,
                row.Slug,
                row.Excerpt,
                row.Body,
                row.CoverUrl);
    }

    public async Task<Paged<SupportThreadDto>> ListSupportThreadsAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var threads = db.SupportTickets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalised.Status))
        {
            if (!Enum.TryParse<SupportTicketStatus>(normalised.Status, ignoreCase: true, out var status))
            {
                return new Paged<SupportThreadDto>([], 0, normalised.Page, normalised.PageSize);
            }

            threads = threads.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            threads = threads.Where(t => t.Subject.Contains(needle) || t.ContactName.Contains(needle));
        }

        var total = await threads.CountAsync(cancellationToken);

        var rows = await threads
            .OrderByDescending(t => t.UpdatedAtUtc)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(t => new
            {
                t.Id, t.Subject, t.ContactName, t.Status, t.Priority, t.UpdatedAtUtc,
                MessageCount = t.Messages.Count,
            })
            .ToListAsync(cancellationToken);

        return new Paged<SupportThreadDto>(
            [.. rows.Select(t => new SupportThreadDto(
                t.Id.ToString(),
                t.Subject,
                t.ContactName,
                WireFormat.TicketStatus(t.Status),
                WireFormat.TicketPriority(t.Priority),
                t.UpdatedAtUtc,
                t.MessageCount))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<SupportThreadDetailDto?> GetSupportThreadAsync(Guid threadId, CancellationToken cancellationToken)
    {
        var thread = await db.SupportTickets.AsNoTracking()
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == threadId, cancellationToken);

        return thread is null
            ? null
            : new SupportThreadDetailDto(
                thread.Id.ToString(),
                thread.Subject,
                thread.ContactName,
                thread.ContactPhone ?? string.Empty,
                thread.ContactEmail,
                WireFormat.TicketStatus(thread.Status),
                WireFormat.TicketPriority(thread.Priority),
                thread.UpdatedAtUtc,
                [.. thread.Messages
                    .OrderBy(m => m.SentAtUtc)
                    .Select(m => new SupportThreadMessageDto(m.Id.ToString(), m.Body, m.FromSupport, m.SentAtUtc))]);
    }

    public async Task<IReadOnlyList<CannedReplyDto>> ListCannedRepliesAsync(CancellationToken cancellationToken) =>
        await db.CannedReplies.AsNoTracking()
            .OrderBy(r => r.Title)
            .Select(r => new CannedReplyDto(r.Id.ToString(), r.Title, r.Body, r.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<Paged<AuditEntryDto>> ListAuditAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var entries = db.AuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            entries = entries.Where(e =>
                e.ActorName.Contains(needle) || e.Action.Contains(needle) || e.Target.Contains(needle));
        }

        if (normalised.From is { } from) entries = entries.Where(e => e.AtUtc >= from);
        if (normalised.To is { } to) entries = entries.Where(e => e.AtUtc <= to);

        var total = await entries.CountAsync(cancellationToken);

        var rows = await entries
            .OrderByDescending(e => e.AtUtc)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(e => new AuditEntryDto(
                e.Id.ToString(), e.ActorName, e.Action, e.Target, e.AtUtc, e.Ip ?? string.Empty))
            .ToListAsync(cancellationToken);

        return new Paged<AuditEntryDto>(rows, total, normalised.Page, normalised.PageSize);
    }

    public async Task<Paged<AdminUserDto>> ListAdminUsersAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var normalised = query.Normalised();
        var users = db.AdminUsers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalised.Search))
        {
            var needle = normalised.Search.Trim();
            users = users.Where(u => u.Name.Contains(needle) || u.Email.Contains(needle));
        }

        var total = await users.CountAsync(cancellationToken);

        var rows = await users
            .OrderBy(u => u.Name)
            .Skip((normalised.Page - 1) * normalised.PageSize)
            .Take(normalised.PageSize)
            .Select(u => new { u.Id, u.Name, u.Email, u.Role, u.LastLoginAtUtc, u.IsActive })
            .ToListAsync(cancellationToken);

        return new Paged<AdminUserDto>(
            [.. rows.Select(u => new AdminUserDto(
                u.Id.ToString(),
                u.Name,
                u.Email,
                u.Role.ToString().ToLowerInvariant(),
                u.LastLoginAtUtc,
                WireFormat.AdminUserStatus(u.IsActive)))],
            total,
            normalised.Page,
            normalised.PageSize);
    }

    public async Task<IReadOnlyList<ApiKeyDto>> ListApiKeysAsync(CancellationToken cancellationToken) =>
        await db.ApiKeys.AsNoTracking()
            .OrderByDescending(k => k.CreatedAtUtc)
            .Select(k => new ApiKeyDto(
                k.Id.ToString(), k.Label, k.Prefix, k.Scope, k.RevokedAtUtc != null, k.CreatedAtUtc, k.LastUsedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, string>> GetSettingsAsync(string section, CancellationToken cancellationToken) =>
        await db.Settings.AsNoTracking()
            .Where(s => s.Section == section)
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

    // -- aggregates ----------------------------------------------------------

    /// <summary>Orders that count as revenue: everything except the ones that were cancelled.</summary>
    private IQueryable<Order> RevenueOrders() =>
        db.Orders.AsNoTracking().Where(o => o.Status != OrderStatus.Cancelled);

    public async Task<DashboardKpisDto> GetDashboardKpisAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var today = await RevenueOrders()
            .Where(o => o.PlacedAtUtc >= startOfDay)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Revenue = g.Sum(o => o.Subtotal.Amount - o.Discount.Amount + o.Shipping.Amount),
                Count = g.Count(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var month = await RevenueOrders()
            .Where(o => o.PlacedAtUtc >= startOfMonth)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Revenue = g.Sum(o => o.Subtotal.Amount - o.Discount.Amount + o.Shipping.Amount),
                Count = g.Count(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new DashboardKpisDto(
            today?.Revenue ?? 0,
            month?.Revenue ?? 0,
            today?.Count ?? 0,
            month?.Count ?? 0,
            await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, cancellationToken),
            await db.Products.CountAsync(p => p.Stock <= LowStockThreshold, cancellationToken),
            await db.Customers.CountAsync(c => c.CreatedAtUtc >= startOfMonth, cancellationToken),
            await db.SupportTickets.CountAsync(t => t.Status == SupportTicketStatus.Open, cancellationToken));
    }

    /// <summary>
    /// Revenue and order counts per period.
    /// </summary>
    /// <remarks>
    /// The grouping is a <c>GROUP BY</c> in the database, written as SQL because
    /// the truncation of a timestamp to a day has no portable LINQ form here —
    /// see <see cref="PeriodBucket"/> for why. Weeks are rolled up from the
    /// daily result: at most 366 already-summed rows, not the orders behind
    /// them, and there is no portable week-of-year function either.
    /// </remarks>
    public async Task<IReadOnlyList<SalesPointDto>> GetSalesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        ReportGrouping grouping,
        CancellationToken cancellationToken)
    {
        var bucket = PeriodBucket.Expression(db.Database, "PlacedAtUtc", monthly: grouping == ReportGrouping.Month);

        var rows = await db.PeriodTotals
            .FromSqlRaw(
                $$"""
                  SELECT {{bucket}} AS "Bucket",
                         COALESCE(SUM("Subtotal" - "Discount" + "Shipping"), 0) AS "Total",
                         COUNT(*) AS "Count",
                         0 AS "SecondaryCount"
                  FROM orders
                  WHERE "Status" <> 'Cancelled' AND "PlacedAtUtc" >= {0} AND "PlacedAtUtc" <= {1}
                  GROUP BY {{bucket}}
                  """,
                PeriodBucket.Boundary(db.Database, fromUtc),
                PeriodBucket.Boundary(db.Database, toUtc))
            .ToListAsync(cancellationToken);

        var points = rows
            .Select(row => new SalesPointDto(PeriodBucket.Parse(row.Bucket), row.Total, row.Count))
            .OrderBy(point => point.Period)
            .ToList();

        if (grouping != ReportGrouping.Week)
        {
            return points;
        }

        return [.. points
            .GroupBy(point => point.Period.AddDays(-(int)point.Period.DayOfWeek).Date)
            .Select(week => new SalesPointDto(
                new DateTimeOffset(week.Key, TimeSpan.Zero),
                week.Sum(point => point.Revenue),
                week.Sum(point => point.Orders)))
            .OrderBy(point => point.Period)];
    }

    public async Task<IReadOnlyList<StatusCountDto>> GetOrderStatusCountsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.PlacedAtUtc >= fromUtc && o.PlacedAtUtc <= toUtc)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new StatusCountDto(WireFormat.AdminOrderStatus(r.Status), r.Count))];
    }

    public async Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from line in db.OrderLines.AsNoTracking()
            join order in RevenueOrders() on line.OrderId equals order.Id
            where order.PlacedAtUtc >= fromUtc && order.PlacedAtUtc <= toUtc
            group line by new { line.ProductId, line.ProductTitle } into grouped
            orderby grouped.Sum(l => l.Quantity) descending
            select new
            {
                grouped.Key.ProductId,
                grouped.Key.ProductTitle,
                Units = grouped.Sum(l => l.Quantity),
                Revenue = grouped.Sum(l => l.UnitPrice.Amount * l.Quantity),
            })
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.ProductId).ToList();
        var skus = await db.Products.AsNoTracking().IgnoreQueryFilters()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Sku })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new TopProductDto(
            r.ProductId.ToString(),
            r.ProductTitle,
            skus.FirstOrDefault(s => s.Id == r.ProductId)?.Sku ?? string.Empty,
            r.Units,
            r.Revenue))];
    }

    /// <summary>
    /// New sign-ups per month, against how many existing customers ordered in
    /// the same month.
    /// </summary>
    /// <remarks>
    /// Monthly whatever <paramref name="grouping"/> asks for: every screen that
    /// draws this chart draws it by month, and a daily growth curve for a shop
    /// this size would be noise.
    /// </remarks>
    public async Task<IReadOnlyList<CustomerGrowthPointDto>> GetCustomerGrowthAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        ReportGrouping grouping,
        CancellationToken cancellationToken)
    {
        _ = grouping;

        var signupBucket = PeriodBucket.Expression(db.Database, "CreatedAtUtc", monthly: true);
        var orderBucket = PeriodBucket.Expression(db.Database, "PlacedAtUtc", monthly: true);

        var from = PeriodBucket.Boundary(db.Database, fromUtc);
        var to = PeriodBucket.Boundary(db.Database, toUtc);

        var signups = await db.PeriodTotals
            .FromSqlRaw(
                $$"""
                  SELECT {{signupBucket}} AS "Bucket", 0 AS "Total", COUNT(*) AS "Count", 0 AS "SecondaryCount"
                  FROM customers
                  WHERE "CreatedAtUtc" >= {0} AND "CreatedAtUtc" <= {1}
                  GROUP BY {{signupBucket}}
                  """,
                from,
                to)
            .ToListAsync(cancellationToken);

        // "Returning" is a customer who existed before the window and ordered
        // inside it — counted distinctly, so two orders in a month is one
        // returning customer.
        var returning = await db.PeriodTotals
            .FromSqlRaw(
                $$"""
                  SELECT {{orderBucket}} AS "Bucket", 0 AS "Total", 0 AS "Count",
                         COUNT(DISTINCT o."CustomerId") AS "SecondaryCount"
                  FROM orders o
                  JOIN customers c ON c."Id" = o."CustomerId"
                  WHERE o."Status" <> 'Cancelled'
                    AND o."PlacedAtUtc" >= {0} AND o."PlacedAtUtc" <= {1}
                    AND c."CreatedAtUtc" < {0}
                  GROUP BY {{orderBucket}}
                  """,
                from,
                to)
            .ToListAsync(cancellationToken);

        return [.. signups.Select(row => row.Bucket)
            .Union(returning.Select(row => row.Bucket))
            .OrderBy(bucket => bucket, StringComparer.Ordinal)
            .Select(bucket => new CustomerGrowthPointDto(
                PeriodBucket.Parse(bucket),
                signups.FirstOrDefault(row => row.Bucket == bucket)?.Count ?? 0,
                returning.FirstOrDefault(row => row.Bucket == bucket)?.SecondaryCount ?? 0))];
    }

    public async Task<StockLevelsDto> GetStockLevelsAsync(CancellationToken cancellationToken)
    {
        var levels = await db.Products.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                InStock = g.Count(p => p.Stock > LowStockThreshold),
                LowStock = g.Count(p => p.Stock > 0 && p.Stock <= LowStockThreshold),
                OutOfStock = g.Count(p => p.Stock == 0),
                // Valued at cost where a cost is known, at selling price
                // otherwise — an inventory value based on the selling price
                // alone overstates what the shop actually has tied up.
                Value = g.Sum(p => (p.CostPrice.Amount == 0 ? p.Price.Amount : p.CostPrice.Amount) * p.Stock),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new StockLevelsDto(
            levels?.InStock ?? 0,
            levels?.LowStock ?? 0,
            levels?.OutOfStock ?? 0,
            levels?.Value ?? 0);
    }

    public async Task<IReadOnlyList<CampaignPerformanceDto>> GetCampaignPerformanceAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var rows = await db.Campaigns.AsNoTracking()
            .Where(c => c.StartsAtUtc == null || (c.StartsAtUtc <= toUtc && (c.EndsAtUtc == null || c.EndsAtUtc >= fromUtc)))
            .Select(c => new { c.Id, c.Title, c.Reach, c.Conversion })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(c => new CampaignPerformanceDto(
            c.Id.ToString(),
            c.Title,
            c.Reach,
            c.Conversion,
            c.Reach == 0 ? 0 : Math.Round((double)c.Conversion / c.Reach * 100, 2)))];
    }

    public async Task<FinancialTotalsDto> GetFinancialTotalsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var totals = await RevenueOrders()
            .Where(o => o.PlacedAtUtc >= fromUtc && o.PlacedAtUtc <= toUtc)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Gross = g.Sum(o => o.Subtotal.Amount),
                Discounts = g.Sum(o => o.Discount.Amount),
                Shipping = g.Sum(o => o.Shipping.Amount),
                Orders = g.Count(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Cost of goods comes from the line's product, valued at today's cost
        // price. A historical cost would need to be captured on the line at
        // order time; until it is, this is an estimate and is named as one in
        // the report screen rather than presented as an audited figure.
        var cost = await (
            from line in db.OrderLines.AsNoTracking()
            join order in RevenueOrders() on line.OrderId equals order.Id
            join product in db.Products.AsNoTracking().IgnoreQueryFilters() on line.ProductId equals product.Id
            where order.PlacedAtUtc >= fromUtc && order.PlacedAtUtc <= toUtc
            select (long?)(product.CostPrice.Amount * line.Quantity))
            .SumAsync(cancellationToken) ?? 0L;

        var gross = totals?.Gross ?? 0;
        var discounts = totals?.Discounts ?? 0;
        var shipping = totals?.Shipping ?? 0;
        var net = gross - discounts + shipping;

        return new FinancialTotalsDto(gross, discounts, shipping, net, cost, net - shipping - cost, totals?.Orders ?? 0);
    }
}
