using Bojan.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.Property(c => c.Phone).HasMaxLength(11).IsRequired();
        // The sign-in key — one account per number, matching the OTP flow's
        // "same phone, same customer" assumption.
        builder.HasIndex(c => c.Phone).IsUnique();

        // The shop's own reference. Unique because it is quoted back — an
        // operator reading «BZ-00042» off an order has to reach one customer.
        builder.Property(c => c.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();

        builder.Property(c => c.FirstName).HasMaxLength(100);
        builder.Property(c => c.LastName).HasMaxLength(100);
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.NationalId).HasMaxLength(10);
        builder.Property(c => c.Group).HasMaxLength(100);
        builder.Property(c => c.AvatarUrl).HasMaxLength(1000);

        builder.MapMoney(c => c.WalletBalance, "WalletBalance");

        // EF Core finds the `_addresses` backing field by naming convention,
        // so the read-only `Addresses` collection needs no accessor exposed.
        builder.HasMany(c => c.Addresses)
            .WithOne()
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
