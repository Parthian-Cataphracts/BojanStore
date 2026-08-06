using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.ToTable("return_requests");

        builder.Property(r => r.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();

        builder.Property(r => r.OrderNumber).HasMaxLength(20);
        builder.Property(r => r.Reason).HasMaxLength(300);
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.RefundMethod).HasMaxLength(50);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.ReviewNote).HasMaxLength(2000);

        // A complex property rather than a converter, so the sum over an order's
        // refunded returns is one SQL SUM rather than rows fetched and added in
        // C# — see MoneyMapping, and the reason IAdminRepository.SumRefundedReturnsAsync
        // needs it.
        builder.MapMoney(r => r.RefundAmount, "RefundAmount");

        builder.HasIndex(r => r.CustomerId);
        builder.HasIndex(r => r.OrderId);

        builder.HasMany(r => r.Items)
            .WithOne()
            .HasForeignKey(i => i.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Timeline)
            .WithOne()
            .HasForeignKey(e => e.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReturnItemConfiguration : IEntityTypeConfiguration<ReturnItem>
{
    public void Configure(EntityTypeBuilder<ReturnItem> builder)
    {
        builder.ToTable("return_items");

        builder.Property(i => i.ProductSlug).HasMaxLength(200);
        builder.Property(i => i.ProductTitle).HasMaxLength(300);
        builder.Property(i => i.ProductImageUrl).HasMaxLength(1000);
    }
}

public sealed class ReturnTimelineEventConfiguration : IEntityTypeConfiguration<ReturnTimelineEvent>
{
    public void Configure(EntityTypeBuilder<ReturnTimelineEvent> builder)
    {
        builder.ToTable("return_timeline_events");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Reason).HasMaxLength(2000);
    }
}
