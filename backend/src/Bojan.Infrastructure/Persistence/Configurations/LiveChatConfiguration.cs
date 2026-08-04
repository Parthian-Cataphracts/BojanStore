using Bojan.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class LiveChatMessageConfiguration : IEntityTypeConfiguration<LiveChatMessage>
{
    public void Configure(EntityTypeBuilder<LiveChatMessage> builder)
    {
        builder.ToTable("live_chat_messages");

        builder.Property(m => m.Body).HasMaxLength(4000);

        // The widget reads one visitor's conversation in order; the panel's
        // list groups by visitor and needs each visitor's unread count.
        builder.HasIndex(m => new { m.VisitorId, m.SentAtUtc });
        builder.HasIndex(m => new { m.VisitorId, m.FromSupport, m.Read });
    }
}
