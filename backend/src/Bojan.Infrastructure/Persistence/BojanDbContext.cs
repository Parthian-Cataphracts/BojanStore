using Bojan.Domain.Admin;
using Bojan.Domain.Business;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Content;
using Bojan.Domain.Customers;
using Bojan.Domain.Identity;
using Bojan.Domain.Inventory;
using Bojan.Domain.Marketing;
using Bojan.Domain.Orders;
using Bojan.Domain.Reviews;
using Bojan.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bojan.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the API.
/// </summary>
/// <remarks>
/// One context for the whole schema rather than one per bounded context: at
/// this size a second context would only mean a second migration history to
/// keep straight for no isolation actually gained — nothing here needs a
/// separate connection or deployment.
/// </remarks>
public sealed class BojanDbContext(DbContextOptions<BojanDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<RecentlyViewedItem> RecentlyViewedItems => Set<RecentlyViewedItem>();
    public DbSet<SearchHistoryEntry> SearchHistoryEntries => Set<SearchHistoryEntry>();
    public DbSet<CustomerNotification> CustomerNotifications => Set<CustomerNotification>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<CouponGrant> CouponGrants => Set<CouponGrant>();

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<SettingEntry> Settings => Set<SettingEntry>();
    public DbSet<ReportExport> ReportExports => Set<ReportExport>();
    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductSpec> ProductSpecs => Set<ProductSpec>();
    public DbSet<ProductVariantAxis> ProductVariantAxes => Set<ProductVariantAxis>();
    public DbSet<ProductVariantOption> ProductVariantOptions => Set<ProductVariantOption>();

    public DbSet<ProductSku> ProductSkus => Set<ProductSku>();

    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionProduct> CollectionProducts => Set<CollectionProduct>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleBlock> ArticleBlocks => Set<ArticleBlock>();
    public DbSet<StockAlert> StockAlerts => Set<StockAlert>();

    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<ProductQuestion> ProductQuestions => Set<ProductQuestion>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderTimelineEvent> OrderTimelineEvents => Set<OrderTimelineEvent>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();
    public DbSet<ReturnTimelineEvent> ReturnTimelineEvents => Set<ReturnTimelineEvent>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();

    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();
    public DbSet<CannedReply> CannedReplies => Set<CannedReply>();

    public DbSet<BusinessRequest> BusinessRequests => Set<BusinessRequest>();
    public DbSet<BusinessRequestEvent> BusinessRequestEvents => Set<BusinessRequestEvent>();
    public DbSet<BusinessOrganization> BusinessOrganizations => Set<BusinessOrganization>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<GiftBundle> GiftBundles => Set<GiftBundle>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<NotificationCampaign> NotificationCampaigns => Set<NotificationCampaign>();
    public DbSet<ContentEntry> ContentEntries => Set<ContentEntry>();

    /// <summary>
    /// The shape a grouped time-series query returns. Keyless: it is the result
    /// of a <c>GROUP BY</c>, not a table.
    /// </summary>
    public DbSet<Reporting.PeriodTotal> PeriodTotals => Set<Reporting.PeriodTotal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BojanDbContext).Assembly);

        modelBuilder.Entity<Reporting.PeriodTotal>().HasNoKey().ToView(null);
    }

    /// <summary>
    /// Makes timestamps sortable on SQLite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SQLite has no native <c>timestamptz</c> and refuses to <c>ORDER BY</c> a
    /// <see cref="DateTimeOffset"/> at all. Almost every read in this API is
    /// ordered by one — orders newest first, the audit trail, support threads —
    /// so on the SQLite host that <c>Bojan.Api.Tests</c> uses (see
    /// <c>BojanApiFactory</c>) every one of them would throw, and only there.
    /// </para>
    /// <para>
    /// Storing the value as an ISO 8601 string sorts correctly so long as every
    /// row carries the same offset, which they do: nothing in this system
    /// writes a timestamp that is not <c>DateTimeOffset.UtcNow</c>. The provider
    /// check means PostgreSQL is untouched and keeps real <c>timestamptz</c>
    /// columns, per <c>BACKEND.md</c> section 1.2.
    /// </para>
    /// <para>
    /// The format is EF's own <see cref="DateTimeOffsetToStringConverter"/>
    /// format, and <see cref="Reporting.PeriodBucket"/> depends on that: it
    /// takes the leading characters of the stored text as the day or month a
    /// report groups by.
    /// </para>
    /// </remarks>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Compared by name rather than through IsSqlite(): that extension lives
        // in the SQLite provider package, which is a test-only dependency and
        // has no business being referenced by Infrastructure.
        if (Database.ProviderName != SqliteProviderName)
        {
            return;
        }

        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToStringConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToStringConverter>();
    }

    internal const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";
}
