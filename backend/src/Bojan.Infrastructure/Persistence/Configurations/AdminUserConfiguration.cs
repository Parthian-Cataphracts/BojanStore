using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.ToTable("admin_users");

        builder.Property(u => u.Name).HasMaxLength(200);
        builder.Property(u => u.Email).HasMaxLength(200);
        builder.Property(u => u.Phone).HasMaxLength(11);
        builder.Property(u => u.PasswordHash).HasMaxLength(500);

        // Sign-in accepts either identity (apps/admin/.../admin-auth/login/route.ts),
        // so both need to resolve to exactly one operator.
        builder.HasIndex(u => u.Email).IsUnique();
        // Columns are quoted, case-preserved PascalCase (no snake_case column
        // convention is configured), so the filter has to match that exactly —
        // an unquoted `phone` here folds to lowercase in Postgres and points at
        // a column that does not exist.
        builder.HasIndex(u => u.Phone).IsUnique().HasFilter("\"Phone\" IS NOT NULL");

        // Stored as text rather than an integer so the column reads directly in
        // a database client. This is a storage format, not the wire format —
        // the API's JSON layer is what must emit the panel's lowercase
        // 'owner' | 'product' | 'sales' | 'support' (see Bojan.Domain.Admin.AdminRole).
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Read on every authorised panel request, so it is never left to a
        // shadow property EF would have to materialise the whole row for.
        builder.Property(u => u.SecurityStamp).IsRequired();
    }
}
