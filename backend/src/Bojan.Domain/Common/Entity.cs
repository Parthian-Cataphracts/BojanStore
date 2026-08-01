namespace Bojan.Domain.Common;

/// <summary>
/// Base for every entity with an identity.
/// </summary>
/// <remarks>
/// The frontend treats every id as an opaque string (see
/// <c>apps/storefront/src/lib/api/types.ts</c>), so a GUID serialised as a
/// string satisfies the contract without the API needing to translate
/// anything at the boundary.
/// </remarks>
public abstract class Entity
{
    public Guid Id { get; init; } = Guid.NewGuid();
}

/// <summary>
/// An entity the panel can soft-delete rather than destroy.
/// </summary>
/// <remarks>
/// Products, categories and orders must not lose history when an operator
/// removes them from the panel's lists — <c>DeletedAtUtc</c> is what a global
/// EF Core query filter hides behind, not a real <c>DELETE</c>.
/// </remarks>
public abstract class SoftDeletableEntity : Entity
{
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public bool IsDeleted => DeletedAtUtc is not null;

    public void SoftDelete(DateTimeOffset nowUtc) => DeletedAtUtc = nowUtc;

    public void Restore() => DeletedAtUtc = null;
}
