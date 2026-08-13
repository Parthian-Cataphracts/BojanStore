using Bojan.Application.Administration;
using Bojan.Domain.Admin;
using Bojan.Domain.Business;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
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
    /// <inheritdoc cref="IAdminRepository.FindWalletTopUpAsync"/>
    public async Task<WalletTopUp?> FindWalletTopUpAsync(Guid id, CancellationToken cancellationToken)
    {
        // Locked before it is read, not after. The decision is idempotent only
        // because WalletTopUp.Approve refuses a request that is not pending, and
        // that check is worth nothing if the status it reads was fetched before
        // the racer committed its own approval. Taking the row lock first means
        // the second operator's read happens after the first one's commit and
        // sees Approved. Caller runs this inside a transaction; see
        // AdminOperationsService.DecideWalletTopUpAsync.
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM wallet_top_ups WHERE "Id" = {id} FOR UPDATE""",
                cancellationToken);
        }

        return await db.WalletTopUps.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<Product?> FindProductAsync(Guid id, CancellationToken cancellationToken) =>
        db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc cref="IAdminRepository.FindProductForUpdateAsync"/>
    public async Task<Product?> FindProductForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        // Same statement CheckoutRepository uses to lock a basket's products, so
        // an operator's stocktake and a shopper's order queue behind one another
        // on the same row rather than overwriting each other's count.
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM products WHERE "Id" = {id} FOR UPDATE""",
                cancellationToken);
        }

        return await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

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

    public async Task<IReadOnlyList<Domain.Catalogue.ProductVolumeTier>> ListVolumeTiersAsync(
        Guid productId,
        CancellationToken cancellationToken) =>
        await db.ProductVolumeTiers
            .Where(tier => tier.ProductId == productId)
            .OrderBy(tier => tier.MinimumQuantity)
            .ToListAsync(cancellationToken);

    public void ReplaceVolumeTiers(
        Guid productId,
        IReadOnlyList<Domain.Catalogue.ProductVolumeTier> existing,
        IEnumerable<Domain.Catalogue.ProductVolumeTier> replacement)
    {
        _ = productId;
        db.ProductVolumeTiers.RemoveRange(existing);
        db.ProductVolumeTiers.AddRange(replacement);
    }

    public Task<bool> ProductSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Products.IgnoreQueryFilters()
            .AnyAsync(p => p.Slug == slug && (exceptId == null || p.Id != exceptId), cancellationToken);

    public async Task<IReadOnlyList<string>> ProductSlugFamilyAsync(
        string stem,
        Guid? exceptId,
        CancellationToken cancellationToken) =>
        // StartsWith rather than a hand-built LIKE: EF escapes the pattern, and
        // the unique index on Slug makes this a range scan over one family.
        // Archived products count — their slugs are still taken, and a save
        // that reused one would collide with a row the filter hides.
        await db.Products.IgnoreQueryFilters()
            .Where(p => p.Slug.StartsWith(stem) && (exceptId == null || p.Id != exceptId))
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);

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

    /// <inheritdoc cref="IAdminRepository.FindOrderForUpdateAsync"/>
    public async Task<Order?> FindOrderForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM orders WHERE "Id" = {id} FOR UPDATE""",
                cancellationToken);
        }

        return await db.Orders
            .Include(o => o.Lines)
            .Include(o => o.Timeline)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public void AddReturnTimelineEvent(ReturnTimelineEvent entry) => db.ReturnTimelineEvents.Add(entry);

    /// <inheritdoc cref="IAdminRepository.FindReturnRequestForUpdateAsync"/>
    public async Task<ReturnRequest?> FindReturnRequestForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM return_requests WHERE "Id" = {id} FOR UPDATE""",
                cancellationToken);
        }

        return await db.ReturnRequests
            .Include(r => r.Items)
            .Include(r => r.Timeline)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    /// <inheritdoc cref="IAdminRepository.SumRefundedReturnsAsync"/>
    public async Task<Money> SumRefundedReturnsAsync(
        Guid orderId,
        Guid exceptReturnId,
        CancellationToken cancellationToken)
    {
        // Summed in SQL rather than by loading the rows — BACKEND.md Phase 6.
        // Nullable because SUM over no rows is null, which an order with exactly
        // one return (the common case) always hits.
        var total = await db.ReturnRequests.AsNoTracking()
            .Where(r => r.OrderId == orderId
                && r.Id != exceptReturnId
                && r.Status == ReturnStatus.Refunded)
            .SumAsync(r => (long?)r.RefundAmount.Amount, cancellationToken);

        return new Money(total ?? 0);
    }

    /// <inheritdoc cref="IAdminRepository.FindCustomerForUpdateAsync"/>
    public async Task<Customer?> FindCustomerForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM customers WHERE "Id" = {id} FOR UPDATE""",
                cancellationToken);
        }

        return await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    /// <inheritdoc cref="IAdminRepository.LoadProductsForUpdateAsync"/>
    public async Task<IReadOnlyList<Product>> LoadProductsForUpdateAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        if (db.Database.IsNpgsql())
        {
            // Ordered, as in CheckoutRepository: two transactions touching the
            // same pair of products take their locks in the same sequence and
            // so cannot deadlock each other.
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM products WHERE "Id" = ANY({ids}) ORDER BY "Id" FOR UPDATE""",
                cancellationToken);
        }

        // Archived products are included: an order placed before the product was
        // withdrawn still has stock to give back to it.
        return await db.Products.IgnoreQueryFilters()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc cref="IAdminRepository.LoadSkusForUpdateAsync"/>
    public async Task<IReadOnlyList<ProductSku>> LoadSkusForUpdateAsync(
        IReadOnlyCollection<Guid> skuIds,
        CancellationToken cancellationToken)
    {
        var ids = skuIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM product_skus WHERE "Id" = ANY({ids}) ORDER BY "Id" FOR UPDATE""",
                cancellationToken);
        }

        return await db.ProductSkus.Where(s => ids.Contains(s.Id)).ToListAsync(cancellationToken);
    }

    public void AddWalletTransaction(WalletTransaction transaction) => db.WalletTransactions.Add(transaction);

    public Task<BusinessRequest?> FindBusinessRequestAsync(Guid id, CancellationToken cancellationToken) =>
        db.BusinessRequests.Include(r => r.Timeline).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public void AddBusinessRequestEvent(BusinessRequestEvent entry) => db.BusinessRequestEvents.Add(entry);

    public void AddNotificationCampaign(NotificationCampaign campaign) => db.NotificationCampaigns.Add(campaign);

    public void AddReportExport(ReportExport export) => db.ReportExports.Add(export);

    public async Task<IReadOnlyList<ReportExport>> ListQueuedReportExportsAsync(int limit, CancellationToken cancellationToken) =>
        await db.ReportExports
            .Where(r => r.Status == JobStatus.Queued)
            .OrderBy(r => r.RequestedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<int> ReclaimStalledJobsAsync(DateTimeOffset startedBefore, CancellationToken cancellationToken)
    {
        var exports = await db.ReportExports
            .Where(r => r.Status == JobStatus.Running
                && (r.StartedAtUtc == null || r.StartedAtUtc < startedBefore))
            .ToListAsync(cancellationToken);

        var backups = await db.BackupJobs
            .Where(j => j.Status == JobStatus.Running
                && (j.StartedAtUtc == null || j.StartedAtUtc < startedBefore))
            .ToListAsync(cancellationToken);

        foreach (var export in exports)
        {
            export.Status = JobStatus.Queued;
            export.StartedAtUtc = null;
        }

        foreach (var backup in backups)
        {
            backup.Status = JobStatus.Queued;
            backup.StartedAtUtc = null;
        }

        return exports.Count + backups.Count;
    }

    public Task<ReportExport?> FindReportExportAsync(Guid id, CancellationToken cancellationToken) =>
        db.ReportExports.FirstOrDefaultAsync(export => export.Id == id, cancellationToken);

    public void AddBackupJob(BackupJob job) => db.BackupJobs.Add(job);

    public Task<BackupJob?> FindNextQueuedBackupAsync(CancellationToken cancellationToken) =>
        db.BackupJobs
            .Where(job => job.Status == JobStatus.Queued)
            .OrderBy(job => job.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

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

    public void AddAdminUser(AdminUser user) => db.AdminUsers.Add(user);

    public Task<bool> IsAdminIdentityTakenAsync(
        string email,
        string? phone,
        Guid? excluding,
        CancellationToken cancellationToken) =>
        db.AdminUsers
            .Where(a => excluding == null || a.Id != excluding)
            // ToLower on both sides, exactly as the sign-in lookup compares
            // them — the index is case-sensitive, so this is the only place the
            // two spellings are ever the same identity.
            .AnyAsync(
                a => a.Email.ToLower() == email.ToLower()
                    || (phone != null && a.Phone == phone),
                cancellationToken);

    public Task<int> CountActiveOwnersAsync(CancellationToken cancellationToken) =>
        db.AdminUsers.CountAsync(a => a.Role == AdminRole.Owner && a.IsActive, cancellationToken);

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
}
