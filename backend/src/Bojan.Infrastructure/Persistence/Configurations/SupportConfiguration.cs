using Bojan.Domain.Admin;
using Bojan.Domain.Customers;
using Bojan.Domain.Support;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("support_tickets");

        // Both optional — the contact form accepts an anonymous sender, and a
        // thread nobody has picked up yet has no assignee.
        builder.HasOne<Customer>().WithMany().HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(t => t.AssigneeId).OnDelete(DeleteBehavior.SetNull);

        builder.Property(t => t.ContactName).HasMaxLength(150);
        builder.Property(t => t.ContactPhone).HasMaxLength(20);
        builder.Property(t => t.ContactEmail).HasMaxLength(200);
        builder.Property(t => t.Subject).HasMaxLength(300);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);

        // The customer reads their own threads newest-first; the panel's queue
        // reads open ones across everyone.
        builder.HasIndex(t => new { t.CustomerId, t.UpdatedAtUtc });
        builder.HasIndex(t => new { t.Status, t.UpdatedAtUtc });

        builder.HasMany(t => t.Messages)
            .WithOne()
            .HasForeignKey(m => m.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SupportMessageConfiguration : IEntityTypeConfiguration<SupportMessage>
{
    public void Configure(EntityTypeBuilder<SupportMessage> builder)
    {
        builder.ToTable("support_messages");

        builder.Property(m => m.Body).HasMaxLength(8000);
        builder.HasIndex(m => new { m.TicketId, m.SentAtUtc });
    }
}

public sealed class CannedReplyConfiguration : IEntityTypeConfiguration<CannedReply>
{
    public void Configure(EntityTypeBuilder<CannedReply> builder)
    {
        builder.ToTable("canned_replies");

        builder.Property(r => r.Title).HasMaxLength(200);
        builder.Property(r => r.Body).HasMaxLength(8000);

        builder.HasQueryFilter(r => r.DeletedAtUtc == null);
    }
}
