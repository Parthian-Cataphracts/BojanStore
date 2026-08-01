using Bojan.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses");

        builder.Property(a => a.Title).HasMaxLength(100);
        builder.Property(a => a.Recipient).HasMaxLength(200);
        builder.Property(a => a.Phone).HasMaxLength(11);
        builder.Property(a => a.Province).HasMaxLength(100);
        builder.Property(a => a.City).HasMaxLength(100);
        builder.Property(a => a.PostalCode).HasMaxLength(10);
        builder.Property(a => a.Line).HasMaxLength(500);

        builder.HasIndex(a => a.CustomerId);
    }
}
