using Bojan.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.Property(p => p.Slug).HasMaxLength(200).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();

        builder.Property(p => p.Sku).HasMaxLength(50);
        builder.HasIndex(p => p.Sku);

        builder.Property(p => p.Title).HasMaxLength(300);
        builder.Property(p => p.ImageUrl).HasMaxLength(1000);
        builder.Property(p => p.ImageAlt).HasMaxLength(300);

        // Required money is a complex property so SQL can sum and order by it;
        // optional money keeps the converter. See MoneyMapping for why.
        builder.MapMoney(p => p.Price, "Price");
        builder.MapMoney(p => p.CostPrice, "CostPrice");
        builder.Property(p => p.CompareAtPrice).HasConversion<MoneyValueConverter>();

        // Products are filtered and sorted by these on every catalogue
        // listing (BACKEND.md Phase 2) — worth an index each.
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.BrandId);

        // The price index is created in the migration rather than here: EF's
        // fluent HasIndex takes a property of the entity, and Price.Amount
        // belongs to a complex type. The column is an ordinary bigint, so the
        // index is an ordinary one — see IX_products_Price in the migration.

        builder.HasMany(p => p.Gallery)
            .WithOne()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Specs)
            .WithOne()
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(p => p.DeletedAtUtc == null);
    }
}

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images");
        builder.Property(i => i.Url).HasMaxLength(1000);
    }
}

public sealed class ProductSpecConfiguration : IEntityTypeConfiguration<ProductSpec>
{
    public void Configure(EntityTypeBuilder<ProductSpec> builder)
    {
        builder.ToTable("product_specs");
        builder.Property(s => s.Label).HasMaxLength(200);
        builder.Property(s => s.Value).HasMaxLength(500);
    }
}
