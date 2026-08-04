using Bojan.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.Property(o => o.Number).HasMaxLength(20).IsRequired();
        builder.HasIndex(o => o.Number).IsUnique();

        // Guards the same double-submit at the database that the application
        // layer guards at the API — BACKEND.md Phase 4, rule 7.
        builder.Property(o => o.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(o => o.IdempotencyKey).IsUnique();

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(o => o.ShippingAddressSnapshot).HasMaxLength(1000);
        builder.Property(o => o.ShippingMethodName).HasMaxLength(200);
        builder.Property(o => o.PaymentMethodName).HasMaxLength(200);
        builder.Property(o => o.CouponCode).HasMaxLength(50);
        builder.Property(o => o.Note).HasMaxLength(2000);
        builder.Property(o => o.DeliveryWindow).HasMaxLength(200);
        builder.Property(o => o.TrackingCode).HasMaxLength(100);

        // Filtered unique: only delivered orders carry a number, and every
        // undelivered order carries the same null. A plain unique index would
        // be fine on PostgreSQL — it treats nulls as distinct — but saying so
        // explicitly keeps the intent readable and survives a provider that
        // does not. The index is also what guarantees uniqueness outright, so
        // OrderNumber.NewInvoiceNumber needs no re-draw loop behind it.
        builder.Property(o => o.InvoiceNumber).HasMaxLength(16);
        builder.HasIndex(o => o.InvoiceNumber)
            .IsUnique()
            .HasFilter("\"InvoiceNumber\" IS NOT NULL");
        builder.Property(o => o.PaymentUrl).HasMaxLength(1000);

        builder.MapMoney(o => o.Subtotal, "Subtotal");
        builder.MapMoney(o => o.Discount, "Discount");
        builder.MapMoney(o => o.Shipping, "Shipping");

        // Stored, unlike Total: what the wallet actually paid at placement is a
        // fact about that moment, and the balance has moved on since. A refund
        // has to return what was taken, which nothing else here still knows.
        builder.MapMoney(o => o.WalletPaid, "WalletPaid");

        // Total is computed (Subtotal - Discount + Shipping), not stored —
        // storing it would let it disagree with its own inputs. PayableOnline
        // is Total less WalletPaid, and computed for the same reason.
        builder.Ignore(o => o.Total);
        builder.Ignore(o => o.PayableOnline);

        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => new { o.CustomerId, o.Status });

        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Timeline)
            .WithOne()
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("order_lines");

        builder.Property(l => l.ProductSlug).HasMaxLength(200);
        builder.Property(l => l.ProductTitle).HasMaxLength(300);
        builder.Property(l => l.ProductImageUrl).HasMaxLength(1000);
        builder.MapMoney(l => l.UnitPrice, "UnitPrice");

        builder.HasIndex(l => l.ProductId);
        builder.HasIndex(l => l.SkuId);
    }
}

public sealed class OrderTimelineEventConfiguration : IEntityTypeConfiguration<OrderTimelineEvent>
{
    public void Configure(EntityTypeBuilder<OrderTimelineEvent> builder)
    {
        builder.ToTable("order_timeline_events");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
    }
}

public sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupons");

        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();

        builder.Property(c => c.AmountOff).HasConversion<MoneyValueConverter>();
        builder.Property(c => c.MinimumSpend).HasConversion<MoneyValueConverter>();
    }
}

public sealed class ShippingMethodConfiguration : IEntityTypeConfiguration<ShippingMethod>
{
    public void Configure(EntityTypeBuilder<ShippingMethod> builder)
    {
        builder.ToTable("shipping_methods");

        // Code, not Id, is what the checkout submits — see ShippingMethod.Code.
        builder.Property(m => m.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(m => m.Code).IsUnique();

        builder.Property(m => m.Title).HasMaxLength(200);
        builder.Property(m => m.Estimate).HasMaxLength(200);
        builder.Property(m => m.Icon).HasMaxLength(50);
        builder.MapMoney(m => m.Price, "Price");
    }
}

public sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("payment_methods");

        builder.Property(m => m.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(m => m.Code).IsUnique();

        builder.Property(m => m.Title).HasMaxLength(200);
        builder.Property(m => m.Note).HasMaxLength(200);
        builder.Property(m => m.Icon).HasMaxLength(50);
    }
}
