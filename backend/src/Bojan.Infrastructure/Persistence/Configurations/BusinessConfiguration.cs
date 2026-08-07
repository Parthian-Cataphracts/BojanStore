using Bojan.Domain.Admin;
using Bojan.Domain.Business;
using Bojan.Domain.Customers;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class BusinessRequestConfiguration : IEntityTypeConfiguration<BusinessRequest>
{
    public void Configure(EntityTypeBuilder<BusinessRequest> builder)
    {
        builder.ToTable("business_requests");

        builder.HasOne<Customer>().WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(r => r.AssigneeId).OnDelete(DeleteBehavior.SetNull);

        builder.Property(r => r.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();

        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Title).HasMaxLength(300);
        builder.Property(r => r.Organization).HasMaxLength(200);
        builder.Property(r => r.ContactName).HasMaxLength(150);
        builder.Property(r => r.Phone).HasMaxLength(20);
        builder.Property(r => r.Email).HasMaxLength(200);
        builder.Property(r => r.Description).HasMaxLength(4000);
        builder.Property(r => r.Deadline).HasMaxLength(100);
        builder.Property(r => r.InternalNote).HasMaxLength(4000);

        builder.HasIndex(r => r.CustomerId);
        builder.HasIndex(r => new { r.Status, r.CreatedAtUtc });

        builder.HasMany(r => r.Timeline)
            .WithOne()
            .HasForeignKey(e => e.BusinessRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BusinessRequestEventConfiguration : IEntityTypeConfiguration<BusinessRequestEvent>
{
    public void Configure(EntityTypeBuilder<BusinessRequestEvent> builder)
    {
        builder.ToTable("business_request_events");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
    }
}

public sealed class BusinessOrganizationConfiguration : IEntityTypeConfiguration<BusinessOrganization>
{
    public void Configure(EntityTypeBuilder<BusinessOrganization> builder)
    {
        builder.ToTable("business_organizations");

        builder.HasOne<Customer>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(o => o.Name).HasMaxLength(200);
        builder.Property(o => o.RegistrationNumber).HasMaxLength(50);
        builder.Property(o => o.EconomicCode).HasMaxLength(50);
        builder.Property(o => o.Province).HasMaxLength(100);
        builder.Property(o => o.City).HasMaxLength(100);
        builder.Property(o => o.Address).HasMaxLength(1000);
        builder.Property(o => o.Phone).HasMaxLength(20);
        builder.Property(o => o.Email).HasMaxLength(200);

        // One profile per customer — screen 68 edits it in place.
        builder.HasIndex(o => o.CustomerId).IsUnique();
    }
}

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes");

        builder.HasOne<BusinessRequest>().WithMany().HasForeignKey(q => q.BusinessRequestId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(q => q.Number).HasMaxLength(30).IsRequired();
        builder.HasIndex(q => q.Number).IsUnique();

        builder.Property(q => q.RequestCode).HasMaxLength(20);
        builder.Property(q => q.Organization).HasMaxLength(200);
        builder.Property(q => q.SalesRep).HasMaxLength(150);
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(20);
        builder.MapMoney(q => q.Discount, "Discount");

        // Subtotal, Tax and Total are computed from the lines — storing any of
        // them would let a quote's own arithmetic disagree with itself.
        builder.Ignore(q => q.Subtotal);
        builder.Ignore(q => q.Tax);
        builder.Ignore(q => q.Total);

        builder.HasIndex(q => q.BusinessRequestId);

        builder.HasMany(q => q.Lines)
            .WithOne()
            .HasForeignKey(l => l.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuoteLineConfiguration : IEntityTypeConfiguration<QuoteLine>
{
    public void Configure(EntityTypeBuilder<QuoteLine> builder)
    {
        builder.ToTable("quote_lines");

        builder.Property(l => l.Title).HasMaxLength(300);
        builder.Property(l => l.Sku).HasMaxLength(50);
        builder.MapMoney(l => l.UnitPrice, "UnitPrice");
    }
}

public sealed class GiftBundleConfiguration : IEntityTypeConfiguration<GiftBundle>
{
    public void Configure(EntityTypeBuilder<GiftBundle> builder)
    {
        builder.ToTable("gift_bundles");

        builder.Property(b => b.Slug).HasMaxLength(200).IsRequired();
        builder.HasIndex(b => b.Slug).IsUnique();

        builder.Property(b => b.Title).HasMaxLength(300);
        builder.Property(b => b.Summary).HasMaxLength(1000);
        builder.Property(b => b.CoverUrl).HasMaxLength(1000);
        builder.Property(b => b.Category).HasMaxLength(100);
        builder.MapMoney(b => b.PricePerUnit, "PricePerUnit");

        builder.HasQueryFilter(b => b.DeletedAtUtc == null);
    }
}
