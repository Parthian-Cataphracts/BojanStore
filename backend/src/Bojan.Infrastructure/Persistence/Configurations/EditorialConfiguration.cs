using Bojan.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.ToTable("collections");

        builder.Property(c => c.Slug).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Title).HasMaxLength(300);
        builder.Property(c => c.Summary).HasMaxLength(1000);
        builder.Property(c => c.CoverUrl).HasMaxLength(1000);
        builder.Property(c => c.EditorialNote).HasMaxLength(4000);

        builder.HasMany(c => c.Products)
            .WithOne()
            .HasForeignKey(p => p.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(c => c.DeletedAtUtc == null);
    }
}

public sealed class CollectionProductConfiguration : IEntityTypeConfiguration<CollectionProduct>
{
    public void Configure(EntityTypeBuilder<CollectionProduct> builder)
    {
        builder.ToTable("collection_products");

        // One membership per product per collection; the panel's editor adds
        // by picking, and picking the same product twice is a mistake, not a
        // second slot.
        builder.HasIndex(p => new { p.CollectionId, p.ProductId }).IsUnique();
        builder.HasIndex(p => p.ProductId);
    }
}

public sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("articles");

        builder.Property(a => a.Slug).HasMaxLength(200).IsRequired();
        builder.HasIndex(a => a.Slug).IsUnique();

        builder.Property(a => a.Title).HasMaxLength(300);
        builder.Property(a => a.Excerpt).HasMaxLength(1000);
        builder.Property(a => a.Category).HasMaxLength(100);
        builder.Property(a => a.CoverUrl).HasMaxLength(1000);
        builder.Property(a => a.RecommendedProductSlug).HasMaxLength(200);

        // The magazine lists by category and always orders by date.
        builder.HasIndex(a => a.Category);
        builder.HasIndex(a => a.PublishedAtUtc);

        builder.HasMany(a => a.Blocks)
            .WithOne()
            .HasForeignKey(b => b.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(a => a.DeletedAtUtc == null);
    }
}

public sealed class ArticleBlockConfiguration : IEntityTypeConfiguration<ArticleBlock>
{
    public void Configure(EntityTypeBuilder<ArticleBlock> builder)
    {
        builder.ToTable("article_blocks");
        builder.Property(b => b.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.Text).HasMaxLength(8000);
    }
}

public sealed class ProductVariantAxisConfiguration : IEntityTypeConfiguration<ProductVariantAxis>
{
    public void Configure(EntityTypeBuilder<ProductVariantAxis> builder)
    {
        builder.ToTable("product_variant_axes");

        builder.Property(a => a.Key).HasMaxLength(50);
        builder.Property(a => a.Label).HasMaxLength(100);
        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(a => a.ProductId);

        builder.HasMany(a => a.Options)
            .WithOne()
            .HasForeignKey(o => o.AxisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProductVariantOptionConfiguration : IEntityTypeConfiguration<ProductVariantOption>
{
    public void Configure(EntityTypeBuilder<ProductVariantOption> builder)
    {
        builder.ToTable("product_variant_options");

        builder.Property(o => o.Key).HasMaxLength(50);
        builder.Property(o => o.Label).HasMaxLength(100);
        builder.Property(o => o.Hex).HasMaxLength(9);
    }
}

public sealed class StockAlertConfiguration : IEntityTypeConfiguration<StockAlert>
{
    public void Configure(EntityTypeBuilder<StockAlert> builder)
    {
        builder.ToTable("stock_alerts");

        builder.Property(a => a.Phone).HasMaxLength(11);
        builder.Property(a => a.Email).HasMaxLength(200);

        // Restocking a product means finding everyone still waiting on it.
        builder.HasIndex(a => new { a.ProductId, a.NotifiedAtUtc });
    }
}
