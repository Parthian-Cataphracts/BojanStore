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
