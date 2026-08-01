using Bojan.Domain.Common;

namespace Bojan.Domain.Catalogue;

/// <summary>
/// A product category, optionally nested one level under a parent.
/// </summary>
/// <remarks>
/// Mirrors the frontend's <c>Category</c> DTO — see
/// <c>apps/storefront/src/lib/api/types.ts</c>. <c>ProductCount</c> is not
/// stored; it is a query projection (Phase 2), listed here only so the shape
/// is not forgotten when that endpoint is built.
/// </remarks>
public sealed class Category : SoftDeletableEntity
{
    public required string Slug { get; set; }

    public required string Name { get; set; }

    /// <summary>Material Symbols icon name shown on the category tile.</summary>
    public string Icon { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public Guid? ParentId { get; set; }

    public bool IsPublished { get; set; } = true;
}
