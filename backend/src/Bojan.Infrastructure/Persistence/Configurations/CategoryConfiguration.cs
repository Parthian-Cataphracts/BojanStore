using Bojan.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        // A subtree may not be orphaned by removing the node above it.
        builder.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Slug).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.Icon).HasMaxLength(100);
        builder.Property(c => c.MetaTitle).HasMaxLength(300);
        builder.Property(c => c.MetaDescription).HasMaxLength(500);

        // The order the tiles and the menu are drawn in, so every read of
        // either sorts by it before falling back to the name.
        builder.HasIndex(c => c.SortOrder);

        // Soft-deleted rows never appear in a normal query — every catalogue
        // read filters them out without every call site remembering to ask.
        builder.HasQueryFilter(c => c.DeletedAtUtc == null);
    }
}
