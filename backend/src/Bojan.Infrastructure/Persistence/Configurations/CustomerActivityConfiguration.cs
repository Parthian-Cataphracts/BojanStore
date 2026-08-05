using Bojan.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("wishlist_items");

        // Saving the same product twice is one heart, not two rows.
        builder.HasIndex(i => new { i.CustomerId, i.ProductId }).IsUnique();
    }
}

public sealed class RecentlyViewedItemConfiguration : IEntityTypeConfiguration<RecentlyViewedItem>
{
    public void Configure(EntityTypeBuilder<RecentlyViewedItem> builder)
    {
        builder.ToTable("recently_viewed_items");

        // Re-opening a product moves the timestamp rather than adding a row,
        // so the pair is unique and the list is ordered by when, not how often.
        builder.HasIndex(i => new { i.CustomerId, i.ProductId }).IsUnique();
        builder.HasIndex(i => new { i.CustomerId, i.ViewedAtUtc });
    }
}

public sealed class SearchHistoryEntryConfiguration : IEntityTypeConfiguration<SearchHistoryEntry>
{
    public void Configure(EntityTypeBuilder<SearchHistoryEntry> builder)
    {
        builder.ToTable("search_history_entries");

        builder.Property(e => e.Term).HasMaxLength(200);
        builder.HasIndex(e => new { e.CustomerId, e.SearchedAtUtc });
    }
}

public sealed class CustomerNotificationConfiguration : IEntityTypeConfiguration<CustomerNotification>
{
    public void Configure(EntityTypeBuilder<CustomerNotification> builder)
    {
        builder.ToTable("customer_notifications");

        builder.Property(n => n.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(n => n.Title).HasMaxLength(200);
        builder.Property(n => n.Body).HasMaxLength(2000);
        builder.Property(n => n.Href).HasMaxLength(500);

        // The bell badge counts unread ones for one customer.
        builder.HasIndex(n => new { n.CustomerId, n.IsRead });

        // One row per customer per campaign, enforced rather than relied upon.
        // The dispatcher skips recipients it has already written, which is what
        // makes a resumed fan-out cheap; this is what makes it correct when two
        // dispatches of the same campaign overlap. Filtered because a
        // notification raised for one customer alone has no campaign, and every
        // one of those carries the same null.
        builder.HasIndex(n => new { n.CustomerId, n.CampaignId })
            .IsUnique()
            .HasFilter("\"CampaignId\" IS NOT NULL");
    }
}

public sealed class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transactions");

        builder.Property(t => t.Title).HasMaxLength(200);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Icon).HasMaxLength(50);

        // Amount is a signed bigint, not a Money conversion: a debit is
        // negative, and Money refuses to be. See WalletTransaction's remarks.
        builder.HasIndex(t => new { t.CustomerId, t.CreatedAtUtc });
    }
}

public sealed class WalletTopUpConfiguration : IEntityTypeConfiguration<WalletTopUp>
{
    public void Configure(EntityTypeBuilder<WalletTopUp> builder)
    {
        builder.ToTable("wallet_top_ups");

        builder.Property(t => t.Amount).HasConversion<MoneyValueConverter>();
        builder.Property(t => t.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.GatewayReference).HasMaxLength(120);
        builder.Property(t => t.ReceiptUrl).HasMaxLength(500);
        builder.Property(t => t.TrackingNumber).HasMaxLength(80);
        builder.Property(t => t.CustomerNote).HasMaxLength(1000);
        builder.Property(t => t.ReviewNote).HasMaxLength(1000);

        // The customer's own history, newest first.
        builder.HasIndex(t => new { t.CustomerId, t.CreatedAtUtc });

        // The operator's review queue: pending first, oldest first.
        builder.HasIndex(t => new { t.Status, t.CreatedAtUtc });

        // A gateway reference identifies one attempt and must never identify
        // two — the callback looks a top-up up by it, and a duplicate would
        // make which one gets credited a matter of row order.
        builder.HasIndex(t => t.GatewayReference).IsUnique();
    }
}

public sealed class CouponGrantConfiguration : IEntityTypeConfiguration<CouponGrant>
{
    public void Configure(EntityTypeBuilder<CouponGrant> builder)
    {
        builder.ToTable("coupon_grants");

        builder.Property(g => g.Title).HasMaxLength(200);
        builder.Property(g => g.Condition).HasMaxLength(300);

        builder.HasIndex(g => new { g.CustomerId, g.CouponId }).IsUnique();
    }
}
