using Bojan.Domain.Customers;
using Bojan.Domain.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("otp_challenges");

        builder.Property(c => c.Phone).HasMaxLength(11).IsRequired();
        builder.Property(c => c.CodeHash).HasMaxLength(64).IsRequired();

        /*
            The lookup, which is always by phone and purpose — and unique,
            which is what makes "a new request supersedes the pending one" a
            fact rather than a hope.

            It was a plain index on Phone alone, with the rule enforced by
            CreateAsync deleting the phone's rows before inserting its own.
            That is a read followed by a write: two sign-in requests for the
            same number arriving together each deleted what they saw and each
            inserted, leaving two live challenges for one phone. FindActiveAsync
            took the newest to survive it, which worked and left the losing row
            behind for good.

            Purpose joined the key once a second use — proving a phone number
            rather than signing in — started writing to the same table: a
            pending login code and a pending verification code for the same
            number are not the same challenge and must not collide.

            The store upserts against this constraint now, so the race resolves
            in one statement and there is nothing left over.
        */
        builder.HasIndex(c => new { c.Phone, c.Purpose }).IsUnique();

        // Where FindActiveForCustomerAsync looks a PhoneVerification challenge
        // up from — the caller's own id, not the candidate phone.
        builder.HasIndex(c => new { c.CustomerId, c.Purpose });
    }
}

/// <summary>
/// A pending password reset — see <see cref="PasswordResetToken"/> for why the
/// token is stored hashed.
/// </summary>
public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasOne<Customer>().WithMany().HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.Cascade);

        // SHA-256, hex — the same 64 characters the OTP challenge stores.
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();

        // The lookup is by hash alone, and it has to be unique or one link
        // could resolve to two customers.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // InvalidateAllAsync sweeps by customer.
        builder.HasIndex(t => t.CustomerId);
    }
}

/// <summary>
/// A pending "confirm this email" link — see
/// <see cref="EmailVerificationToken"/> for why it is stored hashed.
/// </summary>
public sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("email_verification_tokens");

        builder.HasOne<Customer>().WithMany().HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(t => t.Email).HasMaxLength(200).IsRequired();

        // SHA-256, hex — the same 64 characters the password reset token stores.
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();

        // The lookup is by hash alone, and it has to be unique or one link
        // could resolve to two customers.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Where the cooldown check and a customer's own confirm both look up
        // from — the caller's id, not the token.
        builder.HasIndex(t => t.CustomerId);
    }
}
