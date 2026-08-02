using Bojan.Application.Administration;
using Bojan.Domain.Admin;
using Bojan.Domain.Business;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Content;
using Bojan.Domain.Customers;
using Bojan.Domain.Inventory;
using Bojan.Domain.Marketing;
using Bojan.Domain.Orders;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Repositories;

/// <summary>
/// Phase 7's data access.
/// </summary>
/// <remarks>
/// Lookups here use <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters{T}"/>
/// where an archived row still has to be reachable: the panel's list hides
/// soft-deleted products, but restoring one means loading it first, and the
/// global filter would make that impossible.
/// </remarks>
public sealed class AdminRepository(BojanDbContext db) : IAdminRepository
{
    public Task<Product?> FindProductAsync(Guid id, CancellationToken cancellationToken) =>
        db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Product?> FindProductWithDetailAsync(Guid id, CancellationToken cancellationToken) =>
        db.Products.IgnoreQueryFilters()
            .Include(p => p.Gallery)
            .Include(p => p.Specs)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void AddProduct(Product product) => db.Products.Add(product);

    public async Task<IReadOnlyList<ProductVariantAxis>> ListVariantAxesAsync(
        Guid productId, CancellationToken cancellationToken) =>
        await db.ProductVariantAxes
            .Include(axis => axis.Options)
            .Where(axis => axis.ProductId == productId)
            .OrderBy(axis => axis.SortOrder)
            .ToListAsync(cancellationToken);

    public void ReplaceVariantAxes(
        Guid productId,
        IReadOnlyList<ProductVariantAxis> existing,
        IEnumerable<ProductVariantAxis> replacement)
    {
        _ = productId;
        // Options cascade from the axis, so removing the axis is enough.
        db.ProductVariantAxes.RemoveRange(existing);
        db.ProductVariantAxes.AddRange(replacement);
    }

    public async Task<IReadOnlyList<ProductSku>> ListSkusAsync(
        Guid productId, CancellationToken cancellationToken) =>
        await db.ProductSkus
            .Where(sku => sku.ProductId == productId)
            .OrderBy(sku => sku.Code)
            .ToListAsync(cancellationToken);

    public void ReplaceSkus(
        Guid productId,
        IReadOnlyList<ProductSku> existing,
        IEnumerable<ProductSku> replacement)
    {
        _ = productId;
        db.ProductSkus.RemoveRange(existing);
        db.ProductSkus.AddRange(replacement);
    }

    public Task<bool> SkuCodeTakenAsync(
        IReadOnlyList<string> codes, Guid exceptProductId, CancellationToken cancellationToken) =>
        db.ProductSkus.AnyAsync(
            sku => sku.ProductId != exceptProductId && codes.Contains(sku.Code),
            cancellationToken);

    public async Task<IReadOnlyList<ProductAttribute>> ListAttributesAsync(
        Guid productId, CancellationToken cancellationToken) =>
        await db.ProductAttributes
            .Where(attribute => attribute.ProductId == productId)
            .OrderBy(attribute => attribute.SortOrder)
            .ToListAsync(cancellationToken);

    public void ReplaceAttributes(
        Guid productId,
        IReadOnlyList<ProductAttribute> existing,
        IEnumerable<ProductAttribute> replacement)
    {
        _ = productId;
        db.ProductAttributes.RemoveRange(existing);
        db.ProductAttributes.AddRange(replacement);
    }

    public Task<bool> ProductSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Products.IgnoreQueryFilters()
            .AnyAsync(p => p.Slug == slug && (exceptId == null || p.Id != exceptId), cancellationToken);

    public Task<Category?> FindCategoryAsync(Guid id, CancellationToken cancellationToken) =>
        db.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Category?> FindCategoryBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);

    public void AddCategory(Category category) => db.Categories.Add(category);

    public Task<Brand?> FindBrandAsync(Guid id, CancellationToken cancellationToken) =>
        db.Brands.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<Brand?> FindBrandBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Brands.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Slug == slug, cancellationToken);

    public void AddBrand(Brand brand) => db.Brands.Add(brand);

    public Task<Collection?> FindCollectionAsync(Guid id, CancellationToken cancellationToken) =>
        db.Collections.IgnoreQueryFilters()
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void AddCollection(Collection collection) => db.Collections.Add(collection);

    public Task<ContentEntry?> FindContentAsync(Guid id, CancellationToken cancellationToken) =>
        db.ContentEntries.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void AddContent(ContentEntry entry) => db.ContentEntries.Add(entry);

    public Task<Campaign?> FindCampaignAsync(Guid id, CancellationToken cancellationToken) =>
        db.Campaigns.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void AddCampaign(Campaign campaign) => db.Campaigns.Add(campaign);

    public Task<Coupon?> FindCouponAsync(Guid id, CancellationToken cancellationToken) =>
        db.Coupons.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Coupon?> FindCouponByCodeAsync(string code, CancellationToken cancellationToken) =>
        db.Coupons.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);

    public void AddCoupon(Coupon coupon) => db.Coupons.Add(coupon);

    public void AddStockMovement(StockMovement movement) => db.StockMovements.Add(movement);

    public Task<Order?> FindOrderAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.Include(o => o.Timeline).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public void AddOrderTimelineEvent(OrderTimelineEvent entry) => db.OrderTimelineEvents.Add(entry);

    public Task<BusinessRequest?> FindBusinessRequestAsync(Guid id, CancellationToken cancellationToken) =>
        db.BusinessRequests.Include(r => r.Timeline).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public void AddBusinessRequestEvent(BusinessRequestEvent entry) => db.BusinessRequestEvents.Add(entry);

    public void AddNotificationCampaign(NotificationCampaign campaign) => db.NotificationCampaigns.Add(campaign);

    public void AddReportExport(ReportExport export) => db.ReportExports.Add(export);

    public void AddBackupJob(BackupJob job) => db.BackupJobs.Add(job);

    public async Task<IReadOnlyList<BackupJob>> ListBackupJobsAsync(CancellationToken cancellationToken) =>
        await db.BackupJobs.AsNoTracking()
            .OrderByDescending(job => job.RequestedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<BackupJob?> FindBackupJobAsync(Guid id, CancellationToken cancellationToken) =>
        db.BackupJobs.FirstOrDefaultAsync(job => job.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RolePermission>> ListRolePermissionsAsync(CancellationToken cancellationToken) =>
        await db.RolePermissions.AsNoTracking().ToListAsync(cancellationToken);

    public async Task ReplaceRolePermissionsAsync(
        IReadOnlyList<RolePermission> grants, CancellationToken cancellationToken)
    {
        var existing = await db.RolePermissions.ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(existing);
        db.RolePermissions.AddRange(grants);
    }

    public Task<AdminUser?> FindAdminUserAsync(Guid id, CancellationToken cancellationToken) =>
        db.AdminUsers.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<ApiKey?> FindApiKeyAsync(Guid id, CancellationToken cancellationToken) =>
        db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

    public void AddApiKey(ApiKey key) => db.ApiKeys.Add(key);

    public Task<SettingEntry?> FindSettingAsync(string section, string key, CancellationToken cancellationToken) =>
        db.Settings.FirstOrDefaultAsync(s => s.Section == section && s.Key == key, cancellationToken);

    public void AddSetting(SettingEntry entry) => db.Settings.Add(entry);

    public Task<Customer?> FindCustomerAsync(Guid id, CancellationToken cancellationToken) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void AddCustomerNotification(CustomerNotification notification) =>
        db.CustomerNotifications.Add(notification);

    /// <summary>
    /// Resolves an audience name to customer ids.
    /// </summary>
    /// <remarks>
    /// <c>all</c> and an empty audience mean everyone; anything else is matched
    /// against the customer group, which the panel's segments screen defines.
    /// A blocked customer is excluded from every audience — they are not
    /// someone the shop should still be marketing to.
    /// </remarks>
    public async Task<IReadOnlyList<Guid>> ListCustomerIdsAsync(string audience, CancellationToken cancellationToken)
    {
        var query = db.Customers.AsNoTracking().Where(c => !c.IsBlocked);

        if (!string.IsNullOrWhiteSpace(audience) && audience != "all")
        {
            query = query.Where(c => c.Group == audience);
        }

        return await query.Select(c => c.Id).ToListAsync(cancellationToken);
    }
}
