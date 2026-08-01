using Bojan.Domain.Admin;
using Bojan.Domain.Content;
using Bojan.Domain.Inventory;
using Bojan.Domain.Marketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bojan.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");

        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Reason).HasMaxLength(300);
        builder.Property(m => m.Reference).HasMaxLength(100);

        // The movement history for one product, newest first — screen 109.
        builder.HasIndex(m => new { m.ProductId, m.AtUtc });
    }
}

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns");

        builder.Property(c => c.Title).HasMaxLength(300);
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Description).HasMaxLength(4000);

        builder.HasIndex(c => new { c.Status, c.StartsAtUtc });

        builder.HasQueryFilter(c => c.DeletedAtUtc == null);
    }
}

public sealed class NotificationCampaignConfiguration : IEntityTypeConfiguration<NotificationCampaign>
{
    public void Configure(EntityTypeBuilder<NotificationCampaign> builder)
    {
        builder.ToTable("notification_campaigns");

        builder.Property(c => c.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Audience).HasMaxLength(100);
        builder.Property(c => c.Title).HasMaxLength(200);
        builder.Property(c => c.Body).HasMaxLength(4000);

        // The dispatcher's queue: everything scheduled and not yet sent.
        builder.HasIndex(c => new { c.SentAtUtc, c.ScheduledAtUtc });
    }
}

public sealed class ContentEntryConfiguration : IEntityTypeConfiguration<ContentEntry>
{
    public void Configure(EntityTypeBuilder<ContentEntry> builder)
    {
        builder.ToTable("content_entries");

        builder.Property(e => e.Slug).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(300);
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Excerpt).HasMaxLength(1000);
        builder.Property(e => e.Body).HasMaxLength(32000);
        builder.Property(e => e.CoverUrl).HasMaxLength(1000);
        builder.Property(e => e.Author).HasMaxLength(150);

        // Slugs only have to be unique within a kind: a page and an FAQ entry
        // may both be "shipping" without colliding, because they are never
        // resolved from the same route.
        builder.HasIndex(e => new { e.Kind, e.Slug }).IsUnique();

        builder.HasQueryFilter(e => e.DeletedAtUtc == null);
    }
}

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");

        builder.Property(e => e.ActorName).HasMaxLength(150);
        builder.Property(e => e.Action).HasMaxLength(100);
        builder.Property(e => e.Target).HasMaxLength(300);
        builder.Property(e => e.Ip).HasMaxLength(64);

        // Screen 147 reads newest-first, and filters by actor.
        builder.HasIndex(e => e.AtUtc);
        builder.HasIndex(e => new { e.ActorId, e.AtUtc });
    }
}

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        builder.Property(k => k.Label).HasMaxLength(150);
        builder.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.Prefix).HasMaxLength(20);
        builder.Property(k => k.Scope).HasMaxLength(50);

        // Authenticating a machine caller is a lookup by hash.
        builder.HasIndex(k => k.KeyHash).IsUnique();
    }
}

public sealed class SettingEntryConfiguration : IEntityTypeConfiguration<SettingEntry>
{
    public void Configure(EntityTypeBuilder<SettingEntry> builder)
    {
        builder.ToTable("settings");

        builder.Property(s => s.Section).HasMaxLength(50);
        builder.Property(s => s.Key).HasMaxLength(100);
        builder.Property(s => s.Value).HasMaxLength(8000);

        builder.HasIndex(s => new { s.Section, s.Key }).IsUnique();
    }
}

public sealed class ReportExportConfiguration : IEntityTypeConfiguration<ReportExport>
{
    public void Configure(EntityTypeBuilder<ReportExport> builder)
    {
        builder.ToTable("report_exports");

        builder.Property(e => e.Report).HasMaxLength(50);
        builder.Property(e => e.Format).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.FileUrl).HasMaxLength(1000);
        builder.Property(e => e.Error).HasMaxLength(2000);

        builder.HasIndex(e => new { e.Status, e.RequestedAtUtc });
    }
}

public sealed class BackupJobConfiguration : IEntityTypeConfiguration<BackupJob>
{
    public void Configure(EntityTypeBuilder<BackupJob> builder)
    {
        builder.ToTable("backup_jobs");

        builder.Property(j => j.Kind).HasMaxLength(20);
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.FileUrl).HasMaxLength(1000);
        builder.Property(j => j.Error).HasMaxLength(2000);

        builder.HasIndex(j => new { j.Status, j.RequestedAtUtc });
    }
}
