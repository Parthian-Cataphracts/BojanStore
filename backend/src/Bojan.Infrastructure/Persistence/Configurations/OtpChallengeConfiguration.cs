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
