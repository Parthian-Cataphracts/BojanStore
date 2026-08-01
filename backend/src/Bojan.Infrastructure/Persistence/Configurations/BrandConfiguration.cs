using Bojan.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands");

        builder.Property(b => b.Slug).HasMaxLength(200).IsRequired();
        builder.HasIndex(b => b.Slug).IsUnique();

        builder.Property(b => b.Name).HasMaxLength(200);
        builder.Property(b => b.Tagline).HasMaxLength(300);

        builder.HasQueryFilter(b => b.DeletedAtUtc == null);
    }
}
