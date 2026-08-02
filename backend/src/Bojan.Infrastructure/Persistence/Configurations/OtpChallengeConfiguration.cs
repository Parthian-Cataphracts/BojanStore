using Bojan.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("otp_challenges");

        builder.Property(c => c.Phone).HasMaxLength(11).IsRequired();
        builder.Property(c => c.CodeHash).HasMaxLength(64).IsRequired();

        // One active challenge per phone — a new request supersedes rather
        // than stacks, matching AuthService.RequestOtpAsync's "replace" contract.
        builder.HasIndex(c => c.Phone);
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

        // SHA-256, hex — the same 64 characters the OTP challenge stores.
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();

        // The lookup is by hash alone, and it has to be unique or one link
        // could resolve to two customers.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // InvalidateAllAsync sweeps by customer.
        builder.HasIndex(t => t.CustomerId);
    }
}
