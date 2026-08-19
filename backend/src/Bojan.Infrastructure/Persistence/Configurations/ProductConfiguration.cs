using Bojan.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasOne<Brand>().WithMany().HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.Slug).HasMaxLength(200).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();

        builder.Property(p => p.Sku).HasMaxLength(50);
        builder.HasIndex(p => p.Sku);

        builder.Property(p => p.Title).HasMaxLength(300);
        builder.Property(p => p.ImageUrl).HasMaxLength(1000);
        builder.Property(p => p.ImageAlt).HasMaxLength(300);
        builder.Property(p => p.MetaTitle).HasMaxLength(300);
        builder.Property(p => p.MetaDescription).HasMaxLength(500);

        // Derived from TrackStock and AllowBackorder — a stored copy could
        // disagree with the two flags beside it.
        builder.Ignore(p => p.RequiresStockOnHand);

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

        builder.HasMany(p => p.Categories)
            .WithOne()
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(p => p.DeletedAtUtc == null);
    }
}

/// <remarks>
/// Cascade from the product and restrict from the category, matching the pair
/// on the product's own primary <c>CategoryId</c>: deleting a product should
/// take its filing with it, while a category that still has products in it is
/// not something to delete out from under them.
/// </remarks>
public sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("product_categories");

        // The id is assigned in the constructor of Entity, not by the database.
        // Left as store-generated, EF reads a key that is already set as proof
        // the row exists: a filing added to a product loaded from the database
        // was written as an UPDATE that matched nothing, and the save came back
        // a conflict. Saying so explicitly is what makes a new row an INSERT.
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasOne<Category>().WithMany().HasForeignKey(c => c.CategoryId).OnDelete(DeleteBehavior.Restrict);

        // One row per product per category. Picking the same category twice is
        // a mistake, not a second filing.
        builder.HasIndex(c => new { c.ProductId, c.CategoryId }).IsUnique();

        // Browsing a category means finding every product filed under it, so
        // this side is read far more often than the product's own.
        builder.HasIndex(c => c.CategoryId);
    }
}

/// <remarks>
/// <c>ValueGeneratedNever</c>, like the two configurations either side of it —
/// see <see cref="ProductCategoryConfiguration"/>. Without it, adding a second
/// picture to a product that already had one was refused with a conflict: the
/// new row was painted as an update to a row that does not exist, and the
/// update matched nothing.
/// </remarks>
public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images");
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Url).HasMaxLength(1000);
    }
}

public sealed class ProductSpecConfiguration : IEntityTypeConfiguration<ProductSpec>
{
    public void Configure(EntityTypeBuilder<ProductSpec> builder)
    {
        builder.ToTable("product_specs");
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Label).HasMaxLength(200);
        builder.Property(s => s.Value).HasMaxLength(500);
    }
}
