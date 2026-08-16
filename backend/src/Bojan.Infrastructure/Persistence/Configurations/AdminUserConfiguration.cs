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

        /*
          The account this grant sits on, and the only place a password is kept.

          `Cascade`, where it used to be `SetNull`: the column is required now,
          so there is no null to fall back to, and a grant whose account has
          been deleted is a row that can never be signed in as. Deleting the
          person deletes their key to the building, which is the intent
          everywhere else this pairing appears.
        */
        builder.HasOne<Domain.Customers.Customer>()
            .WithMany()
            .HasForeignKey(u => u.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // One grant per account, so a person cannot be appointed twice and two
        // operators cannot place orders as the same customer. Unfiltered now
        // that the column is required.
        builder.HasIndex(u => u.CustomerId).IsUnique();

        // The sections this operator may open. Owned by the grant: revoking
        // somebody's panel access takes their permissions with it rather than
        // leaving rows pointing at nothing.
        builder.HasMany(u => u.Sections)
            .WithOne()
            .HasForeignKey(s => s.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AdminUserSectionConfiguration : IEntityTypeConfiguration<AdminUserSection>
{
    public void Configure(EntityTypeBuilder<AdminUserSection> builder)
    {
        builder.ToTable("admin_user_sections");

        builder.Property(s => s.Section).HasMaxLength(40).IsRequired();

        // One row per operator per section: existence is the grant, so a
        // duplicate would be the same grant twice and a revoke that missed one
        // of them would silently leave access in place.
        builder.HasIndex(s => new { s.AdminUserId, s.Section }).IsUnique();
    }
}
