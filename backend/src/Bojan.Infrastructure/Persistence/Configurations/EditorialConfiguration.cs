using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
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

        // Same as the article's blocks above — the collection's own editor
        // writes these through its navigation.
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.HasOne<Product>().WithMany().HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Cascade);

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

/// <remarks>
/// <c>ValueGeneratedNever</c> for the reason spelled out on
/// <see cref="ProductCategoryConfiguration"/>: the id is assigned in
/// <c>Entity</c>'s own initialiser, and EF reads an id that is already set as
/// proof the row exists. Every block of an article being edited is built fresh
/// — the body is replaced wholesale on each save — so all of them were written
/// as updates to rows that had never been inserted, and the second save of any
/// article came back a conflict. Creating one worked; editing it was
/// impossible.
/// </remarks>
public sealed class ArticleBlockConfiguration : IEntityTypeConfiguration<ArticleBlock>
{
    public void Configure(EntityTypeBuilder<ArticleBlock> builder)
    {
        builder.ToTable("article_blocks");
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.Text).HasMaxLength(8000);
    }
}

public sealed class ProductVariantAxisConfiguration : IEntityTypeConfiguration<ProductVariantAxis>
{
    public void Configure(EntityTypeBuilder<ProductVariantAxis> builder)
    {
        builder.ToTable("product_variant_axes");

        builder.HasOne<Product>().WithMany().HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Cascade);

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

public sealed class ProductSkuConfiguration : IEntityTypeConfiguration<ProductSku>
{
    public void Configure(EntityTypeBuilder<ProductSku> builder)
    {
        builder.ToTable("product_skus");

        builder.HasOne<Product>().WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.Code).HasMaxLength(64);
        builder.Property(s => s.Barcode).HasMaxLength(32);
        builder.Property(s => s.Combination).HasMaxLength(200);

        builder.Property(s => s.Price)
            .HasConversion(money => money.Amount, amount => new Money(amount));

        // Nullable, so "not on sale" is a null rather than a zero that would
        // read as "was free before". Same converter as the product's own
        // CompareAtPrice.
        builder.Property(s => s.CompareAtPrice).HasConversion<MoneyValueConverter>();

        // Derived from the two prices above — a stored copy could disagree
        // with them.
        builder.Ignore(s => s.IsSellable);

        builder.HasIndex(s => s.ProductId);

        // A code identifies a sellable unit; two of them would make an order
        // line ambiguous about what was bought.
        builder.HasIndex(s => s.Code).IsUnique();
    }
}

public sealed class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> builder)
    {
        builder.ToTable("product_attributes");

        builder.HasOne<Product>().WithMany().HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.Name).HasMaxLength(100);
        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Values).HasMaxLength(1000);

        builder.HasIndex(a => a.ProductId);
    }
}

public sealed class ProductVolumeTierConfiguration : IEntityTypeConfiguration<ProductVolumeTier>
{
    public void Configure(EntityTypeBuilder<ProductVolumeTier> builder)
    {
        builder.ToTable("product_volume_tiers");

        builder.HasOne<Product>().WithMany().HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.Cascade);

        // One rung per quantity per product. Two tiers sharing a floor is a
        // contradiction — the pricing has a rule for reading it, but the rule
        // exists to survive bad data rather than to permit it.
        builder.HasIndex(t => new { t.ProductId, t.MinimumQuantity }).IsUnique();
    }
}

public sealed class StockAlertConfiguration : IEntityTypeConfiguration<StockAlert>
{
    public void Configure(EntityTypeBuilder<StockAlert> builder)
    {
        builder.ToTable("stock_alerts");

        builder.HasOne<Product>().WithMany().HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.Phone).HasMaxLength(11);
        builder.Property(a => a.Email).HasMaxLength(200);

        // Restocking a product means finding everyone still waiting on it.
        builder.HasIndex(a => new { a.ProductId, a.NotifiedAtUtc });
    }
}
