using Bojan.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.Property(c => c.Slug).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.Icon).HasMaxLength(100);

        // Soft-deleted rows never appear in a normal query — every catalogue
        // read filters them out without every call site remembering to ask.
        builder.HasQueryFilter(c => c.DeletedAtUtc == null);
    }
}
