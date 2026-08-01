using Bojan.Domain.Common;

namespace Bojan.Domain.Content;

/// <summary>The frontend's <c>ContentEntry.type</c> — <c>'article' | 'page' | 'banner' | 'faq'</c>.</summary>
public enum ContentKind
{
    Article,
    Page,
    Banner,
    Faq,
}

public enum ContentStatus
{
    Draft,
    Published,
}

/// <summary>
/// An editable piece of site content — screens 118-127.
/// </summary>
/// <remarks>
/// One entity for four kinds because that is how the panel treats them: a
/// single <c>content</c> resource with a <c>kind</c> field
/// (<c>apps/admin/src/lib/api/resources.ts</c>), one list screen, one form.
/// Magazine articles are the exception — they have their own storefront DTO
/// with typed body blocks, so <see cref="Catalogue.Article"/> stays separate
/// and a <see cref="ContentKind.Article"/> entry here points at it by slug.
/// </remarks>
public sealed class ContentEntry : SoftDeletableEntity
{
    public required string Slug { get; set; }

    public required string Title { get; set; }

    public required ContentKind Kind { get; set; }

    public string? Excerpt { get; set; }

    public string? Body { get; set; }

    public string? CoverUrl { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    /// <summary>Display name of whoever last wrote it — the panel's list column.</summary>
    public string Author { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
