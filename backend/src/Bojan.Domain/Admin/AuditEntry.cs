using Bojan.Domain.Common;

namespace Bojan.Domain.Admin;

/// <summary>
/// One recorded operator action — screen 147.
/// </summary>
/// <remarks>
/// <c>BACKEND.md</c> Phase 7: "Every write here goes in an audit log." The
/// entry is written in the same transaction as the change it describes, so a
/// successful write with no audit row is impossible.
///
/// Nothing here references the changed entity by foreign key on purpose: an
/// audit trail has to outlive what it describes, including a hard delete, so
/// <see cref="Target"/> is a printed identifier rather than a relation.
/// </remarks>
public sealed class AuditEntry : Entity
{
    public required Guid ActorId { get; init; }

    /// <summary>Operator's display name, captured at write time — an audit row must not change when a name does.</summary>
    public required string ActorName { get; init; }

    /// <summary>What happened, as a stable machine key, e.g. <c>product.pricing.updated</c>.</summary>
    public required string Action { get; init; }

    /// <summary>What it happened to, printed — an id, a slug, an order number.</summary>
    public required string Target { get; init; }

    public string? Ip { get; init; }

    public DateTimeOffset AtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// One section a non-owner role may see — screen 146's grid.
/// </summary>
/// <remarks>
/// Existence is the grant: a row means the role can see the section, absence
/// means it cannot. Owner is never stored here — <c>AuthorizationPolicies</c>
/// already gives it everything, and a row for it would be one more place that
/// could disagree with the guarantee the frontend's locked owner row depends
/// on (<c>RolePermissionMatrix.tsx</c>).
/// </remarks>
public sealed class RolePermission : Entity
{
    /// <summary><c>product</c>, <c>sales</c>, or <c>support</c> — never <c>owner</c>.</summary>
    public required string Role { get; init; }

    /// <summary>A section label as the panel's matrix names it, e.g. <c>سفارش‌ها</c>.</summary>
    public required string Section { get; init; }
}

/// <summary>
/// A key granting machine access to the API — screen 155.
/// </summary>
/// <remarks>
/// Only the hash is stored. A key is shown once, at creation, and can never be
/// read back — the same reason a password hash is not reversible. Revoking
/// sets <see cref="RevokedAtUtc"/> rather than deleting, so an audit entry
/// naming the key still resolves.
/// </remarks>
public sealed class ApiKey : Entity
{
    public required string Label { get; set; }

    /// <summary>Hex SHA-256 of the key. Never the key.</summary>
    public required string KeyHash { get; init; }

    /// <summary>First few characters, kept so the panel can tell two keys apart in a list.</summary>
    public required string Prefix { get; init; }

    /// <summary>What it may reach — <c>read</c>, <c>write</c>, or a named area.</summary>
    public string Scope { get; set; } = "read";

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public DateTimeOffset? LastUsedAtUtc { get; set; }

    public required Guid CreatedById { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// One stored setting value — screens 150-156.
/// </summary>
/// <remarks>
/// The panel writes settings a section at a time
/// (<c>{ section, values }</c>), so the natural key is the pair. Values are
/// stored as JSON text rather than typed columns because the sections have
/// nothing in common and a new one must not need a migration.
/// </remarks>
public sealed class SettingEntry : Entity
{
    public required string Section { get; init; }

    public required string Key { get; init; }

    /// <summary>JSON-encoded value. A string setting is stored as a JSON string, quotes included.</summary>
    public required string Value { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedById { get; set; }
}

public enum ExportFormat
{
    Csv,
    Xlsx,
    Pdf,
}

public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
}

/// <summary>
/// A queued report export — screen 141.
/// </summary>
/// <remarks>
/// <c>BACKEND.md</c> Phase 7: "<c>/reports/export</c> queues and mails a link;
/// it is not a synchronous download." The row is the queue: the write returns
/// as soon as it exists, and a worker fills in <see cref="FileUrl"/>.
/// </remarks>
public sealed class ReportExport : Entity
{
    /// <summary>Which report — <c>sales</c>, <c>orders</c>, <c>inventory</c>, <c>customers</c>, <c>campaigns</c>, <c>financial</c>.</summary>
    public required string Report { get; init; }

    public required ExportFormat Format { get; init; }

    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    public JobStatus Status { get; set; } = JobStatus.Queued;

    public string? FileUrl { get; set; }

    public string? Error { get; set; }

    public required Guid RequestedById { get; init; }

    public DateTimeOffset RequestedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAtUtc { get; set; }
}

/// <summary>A database or media backup — screen 156.</summary>
public sealed class BackupJob : Entity
{
    /// <summary><c>database</c>, <c>media</c>, or <c>full</c>.</summary>
    public required string Kind { get; init; }

    public JobStatus Status { get; set; } = JobStatus.Queued;

    public string? FileUrl { get; set; }

    public long? SizeBytes { get; set; }

    public string? Error { get; set; }

    public required Guid RequestedById { get; init; }

    public DateTimeOffset RequestedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
