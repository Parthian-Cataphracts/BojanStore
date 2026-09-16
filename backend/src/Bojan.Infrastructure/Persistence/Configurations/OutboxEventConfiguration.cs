using Bojan.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("outbox_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventName).HasMaxLength(100);
        builder.Property(e => e.Status).HasMaxLength(20);
        builder.Property(e => e.LastError).HasMaxLength(1000);

        // The dispatcher's query: due Pending rows, oldest first. The index makes
        // that a range scan rather than a table scan as the delivered rows pile up.
        builder.HasIndex(e => new { e.Status, e.NextAttemptUtc });
    }
}
