using Bojan.Domain.Common;

namespace Bojan.Domain.Catalogue;

/// <summary>Mirrors the frontend's <c>Brand</c> DTO — see <c>apps/storefront/src/lib/api/types.ts</c>.</summary>
public sealed class Brand : SoftDeletableEntity
{
    public required string Slug { get; set; }

    public required string Name { get; set; }

    /// <summary>Short positioning line under the brand name on screen 26.</summary>
    public string? Tagline { get; set; }

    public string? Description { get; set; }

    public string? LogoUrl { get; set; }

    public string? CoverUrl { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsPublished { get; set; } = true;
}
