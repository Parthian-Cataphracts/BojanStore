using Bojan.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.ToTable("product_reviews");

        builder.Property(r => r.AuthorName).HasMaxLength(150);
        builder.Property(r => r.Title).HasMaxLength(300);
        builder.Property(r => r.Body).HasMaxLength(4000);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        // The product page reads published reviews for one product; the panel
        // reads pending ones across all products. Both are covered here.
        builder.HasIndex(r => new { r.ProductId, r.Status });
        builder.HasIndex(r => r.CustomerId);

        // One review per customer per product — the same shopper writing twice
        // is an edit, and the write refuses it rather than stacking a second.
        builder.HasIndex(r => new { r.CustomerId, r.ProductId }).IsUnique();
    }
}

public sealed class ProductQuestionConfiguration : IEntityTypeConfiguration<ProductQuestion>
{
    public void Configure(EntityTypeBuilder<ProductQuestion> builder)
    {
        builder.ToTable("product_questions");

        builder.Property(q => q.AuthorName).HasMaxLength(150);
        builder.Property(q => q.Body).HasMaxLength(2000);
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(q => q.AnswerBody).HasMaxLength(4000);
        builder.Property(q => q.AnswerAuthor).HasMaxLength(150);

        builder.HasIndex(q => new { q.ProductId, q.Status });
    }
}
