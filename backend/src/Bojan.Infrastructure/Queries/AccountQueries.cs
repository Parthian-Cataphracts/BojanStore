using Bojan.Application.Accounts;
using Bojan.Application.Catalogue;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Orders;
using Bojan.Domain.Reviews;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Queries;

/// <summary>
/// Phase 3's reads.
/// </summary>
/// <remarks>
/// Every query starts from the customer id, so a row belonging to someone else
/// is never loaded in the first place — that is what makes the "404, not 403"
/// rule (<c>BACKEND.md</c> Phase 3) structural rather than a check to remember.
/// </remarks>
public sealed class AccountQueries(BojanDbContext db, ICatalogueQueries catalogue) : IAccountQueries
{
    public Task<UserDto?> GetProfileAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new UserDto(
                c.Id.ToString(),
                c.FirstName,
                c.LastName,
                c.Phone,
                c.Email,
                c.BirthDate == null ? null : c.BirthDate.Value.ToString("yyyy-MM-dd"),
                c.City,
                c.AvatarUrl,
                c.WalletBalance.Amount,
                c.LoyaltyPoints))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<OrderSummaryDto>> ListOrdersAsync(
        Guid customerId,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = db.Orders.AsNoTracking().Where(o => o.CustomerId == customerId);

        if (WireFormat.ParseOrderStatus(status) is { } parsed)
        {
            query = query.Where(o => o.Status == parsed);
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            // A filter the vocabulary does not have matches nothing, rather
            // than silently returning everything.
            return [];
        }

        var rows = await query
            .OrderByDescending(o => o.PlacedAtUtc)
            .Select(o => new
            {
                o.Id,
                o.Number,
                o.PlacedAtUtc,
                o.Status,
                ItemCount = o.Lines.Count,
                o.Subtotal,
                o.Discount,
                o.Shipping,
                Thumbnails = o.Lines.OrderBy(l => l.ProductTitle).Select(l => l.ProductImageUrl).ToList(),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(o => new OrderSummaryDto(
            o.Id.ToString(),
            o.Number,
            o.PlacedAtUtc,
            WireFormat.StorefrontOrderStatus(o.Status),
            o.ItemCount,
            (o.Subtotal.ClampedMinus(o.Discount) + o.Shipping).Amount,
            o.Thumbnails.Count > 0 ? o.Thumbnails : null))];
    }

    public async Task<OrderDetailDto?> GetOrderAsync(Guid customerId, string idOrNumber, CancellationToken cancellationToken)
    {
        var order = await FindOrderQuery(customerId, idOrNumber)
            .Include(o => o.Lines)
            .Include(o => o.Timeline)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        var reachedAt = order.Timeline
            .GroupBy(step => step.Status)
            .ToDictionary(group => group.Key, group => group.Min(step => step.AtUtc));

        return new OrderDetailDto(
            order.Id.ToString(),
            order.Number,
            order.PlacedAtUtc,
            WireFormat.StorefrontOrderStatus(order.Status),
            order.Lines.Count,
            order.Total.Amount,
            [.. order.Lines.Select(l => l.ProductImageUrl)],
            [.. order.Lines.Select(l => new OrderItemDto(
                l.ProductId.ToString(), l.ProductSlug, l.ProductTitle, l.ProductImageUrl, l.Quantity, l.UnitPrice.Amount))],
            Timelines.ForOrder(order.Status, reachedAt),
            order.ShippingAddressSnapshot,
            order.ShippingMethodName,
            order.PaymentMethodName,
            order.Subtotal.Amount,
            order.Discount.Amount,
            order.Shipping.Amount,
            order.TrackingCode);
    }

    /// <summary>
    /// Resolves whichever identifier the frontend happened to have — the id or
    /// the human-readable number — always scoped to one customer.
    /// </summary>
    private IQueryable<Order> FindOrderQuery(Guid customerId, string idOrNumber)
    {
        var query = db.Orders.Where(o => o.CustomerId == customerId);

        return Guid.TryParse(idOrNumber, out var id)
            ? query.Where(o => o.Id == id)
            : query.Where(o => o.Number == idOrNumber);
    }

    public async Task<IReadOnlyList<AddressDto>> ListAddressesAsync(Guid customerId, CancellationToken cancellationToken) =>
        await db.Addresses.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Title)
            .Select(a => new AddressDto(
                a.Id.ToString(), a.Title, a.Recipient, a.Phone, a.Province, a.City, a.PostalCode, a.Line, a.IsDefault))
            .ToListAsync(cancellationToken);

    public Task<AddressDto?> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken) =>
        db.Addresses.AsNoTracking()
            .Where(a => a.CustomerId == customerId && a.Id == addressId)
            .Select(a => new AddressDto(
                a.Id.ToString(), a.Title, a.Recipient, a.Phone, a.Province, a.City, a.PostalCode, a.Line, a.IsDefault))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<ProductDto>> ListWishlistAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var slugs = await (
            from item in db.WishlistItems.AsNoTracking()
            join product in db.Products.AsNoTracking() on item.ProductId equals product.Id
            where item.CustomerId == customerId
            orderby item.AddedAtUtc descending
            select product.Slug)
            .ToListAsync(cancellationToken);

        return await catalogue.GetProductsBySlugsAsync(slugs, cancellationToken);
    }

    public async Task<IReadOnlyList<ReturnRequestDto>> ListReturnsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var requests = await db.ReturnRequests.AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .Include(r => r.Items)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return [.. requests.Select(ToDto)];
    }

    public async Task<ReturnRequestDto?> GetReturnAsync(Guid customerId, string idOrCode, CancellationToken cancellationToken)
    {
        var query = db.ReturnRequests.AsNoTracking().Where(r => r.CustomerId == customerId);

        query = Guid.TryParse(idOrCode, out var id)
            ? query.Where(r => r.Id == id)
            : query.Where(r => r.Code == idOrCode);

        var request = await query.Include(r => r.Items).FirstOrDefaultAsync(cancellationToken);
        return request is null ? null : ToDto(request);
    }

    /// <summary>
    /// Projects the first item, because the frontend's <c>ReturnRequest</c> DTO
    /// is single-product while the model is not — see
    /// <see cref="ReturnRequest"/>'s remarks for why both are true.
    /// </summary>
    private static ReturnRequestDto ToDto(ReturnRequest request)
    {
        var first = request.Items.FirstOrDefault();

        return new ReturnRequestDto(
            request.Id.ToString(),
            request.Code,
            request.OrderId.ToString(),
            request.OrderNumber,
            first?.ProductSlug ?? string.Empty,
            first?.ProductTitle ?? string.Empty,
            first?.ProductImageUrl ?? string.Empty,
            first?.Quantity ?? 0,
            request.Reason,
            request.Description,
            WireFormat.ReturnStatus(request.Status),
            request.CreatedAtUtc,
            Timelines.ForReturn(request.Status));
    }

    public async Task<IReadOnlyList<NotificationDto>> ListNotificationsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var rows = await db.CustomerNotifications.AsNoTracking()
            .Where(n => n.CustomerId == customerId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new { n.Id, n.Kind, n.Title, n.Body, n.CreatedAtUtc, n.IsRead, n.Href })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(n => new NotificationDto(
            n.Id.ToString(), WireFormat.NotificationKind(n.Kind), n.Title, n.Body, n.CreatedAtUtc, n.IsRead, n.Href))];
    }

    public async Task<IReadOnlyList<SupportTicketDto>> ListTicketsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var rows = await db.SupportTickets.AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.UpdatedAtUtc)
            .Select(t => new
            {
                t.Id,
                t.Subject,
                t.Status,
                t.UpdatedAtUtc,
                // The card preview is the latest message, so only that one is
                // fetched rather than the whole thread.
                Last = t.Messages.OrderByDescending(m => m.SentAtUtc)
                    .Select(m => new { m.Body, m.FromSupport })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(t => new SupportTicketDto(
            t.Id.ToString(),
            t.Subject,
            WireFormat.TicketStatus(t.Status),
            t.Last?.Body ?? string.Empty,
            t.Last?.FromSupport ?? false,
            t.UpdatedAtUtc))];
    }

    public async Task<IReadOnlyList<MyReviewDto>> ListMyReviewsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var rows = await (
            from review in db.ProductReviews.AsNoTracking()
            join product in db.Products.AsNoTracking() on review.ProductId equals product.Id
            where review.CustomerId == customerId
            orderby review.CreatedAtUtc descending
            select new
            {
                review.Id,
                product.Slug,
                product.Title,
                product.ImageUrl,
                review.Rating,
                review.Body,
                review.Status,
                review.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new MyReviewDto(
            r.Id.ToString(),
            r.Slug,
            r.Title,
            r.ImageUrl,
            r.Rating,
            r.Body,
            WireFormat.ReviewStatus(r.Status),
            r.CreatedAtUtc))];
    }

    /// <summary>
    /// Delivered products the customer has not reviewed yet.
    /// </summary>
    /// <remarks>
    /// The anti-join is in SQL: "delivered lines whose product has no review by
    /// this customer". Fetching every delivered line and filtering in C# would
    /// grow with the customer's order history rather than with the answer.
    /// </remarks>
    public async Task<IReadOnlyList<AwaitingReviewDto>> ListAwaitingReviewsAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from order in db.Orders.AsNoTracking()
            from line in order.Lines
            where order.CustomerId == customerId
                && order.Status == OrderStatus.Delivered
                && !db.ProductReviews.Any(r => r.CustomerId == customerId && r.ProductId == line.ProductId)
            orderby order.PlacedAtUtc descending
            select new
            {
                OrderId = order.Id,
                line.ProductSlug,
                line.ProductTitle,
                line.ProductImageUrl,
                DeliveredAt = order.Timeline
                    .Where(e => e.Status == OrderStatus.Delivered)
                    .Select(e => (DateTimeOffset?)e.AtUtc)
                    .FirstOrDefault(),
                order.PlacedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return [.. rows
            .DistinctBy(r => r.ProductSlug)
            .Select(r => new AwaitingReviewDto(
                r.OrderId.ToString(),
                r.ProductSlug,
                r.ProductTitle,
                r.ProductImageUrl,
                r.DeliveredAt ?? r.PlacedAtUtc))];
    }

    public async Task<IReadOnlyList<WalletTransactionDto>> ListWalletTransactionsAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var rows = await db.WalletTransactions.AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new { t.Id, t.Title, t.Amount, t.CreatedAtUtc, t.Status, t.Icon })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(t => new WalletTransactionDto(
            t.Id.ToString(), t.Title, t.Amount, t.CreatedAtUtc, WireFormat.WalletStatus(t.Status), t.Icon))];
    }

    public async Task<IReadOnlyList<CouponDto>> ListCouponsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var rows = await (
            from grant in db.CouponGrants.AsNoTracking()
            join coupon in db.Coupons.AsNoTracking() on grant.CouponId equals coupon.Id
            where grant.CustomerId == customerId
            orderby grant.IsUsed, coupon.ExpiresAtUtc
            select new
            {
                grant.Id,
                coupon.Code,
                grant.Title,
                grant.Condition,
                coupon.PercentOff,
                coupon.AmountOff,
                coupon.ExpiresAtUtc,
                grant.IsUsed,
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(c => new CouponDto(
            c.Id.ToString(),
            c.Code,
            c.Title,
            c.Condition,
            c.PercentOff,
            c.AmountOff?.Amount,
            // A coupon with no expiry still has to render a date; the far
            // future is what "never expires" looks like on screen 59.
            c.ExpiresAtUtc ?? DateTimeOffset.MaxValue,
            c.IsUsed))];
    }

    public async Task<IReadOnlyList<ProductDto>> ListRecentlyViewedAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var slugs = await (
            from item in db.RecentlyViewedItems.AsNoTracking()
            join product in db.Products.AsNoTracking() on item.ProductId equals product.Id
            where item.CustomerId == customerId
            orderby item.ViewedAtUtc descending
            select product.Slug)
            .Take(24)
            .ToListAsync(cancellationToken);

        return await catalogue.GetProductsBySlugsAsync(slugs, cancellationToken);
    }
}
