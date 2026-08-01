using Bojan.Domain.Common;

namespace Bojan.Domain.Catalogue;

/// <summary>
/// A magazine article — screens 28 and 29.
/// </summary>
/// <remarks>
/// Mirrors the frontend's <c>Article</c> DTO. The body is a list of typed
/// blocks rather than a blob of HTML because that is what the frontend
/// renders: <c>ArticleBlock</c> is
/// <c>{ type: 'paragraph' | 'heading' | 'product'; text?: string }</c>, and a
/// <c>product</c> block carries no text at all — it renders
/// <see cref="RecommendedProductSlug"/> in place.
/// </remarks>
public sealed class Article : SoftDeletableEntity
{
    public required string Slug { get; set; }

    public required string Title { get; set; }

    public string Excerpt { get; set; } = string.Empty;

    /// <summary>Editorial category, e.g. "راهنمای خرید" — free text, not a catalogue category.</summary>
    public string Category { get; set; } = string.Empty;

    public string CoverUrl { get; set; } = string.Empty;

    public DateTimeOffset PublishedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Reading time in minutes, shown on the card.</summary>
    public int ReadingMinutes { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsPublished { get; set; } = true;

    /// <summary>Product promoted inside the body by a <c>product</c> block.</summary>
    public string? RecommendedProductSlug { get; set; }

    private readonly List<ArticleBlock> _blocks = [];
    public IReadOnlyCollection<ArticleBlock> Blocks => _blocks;

    public void AddBlock(ArticleBlockKind kind, string? text, int sortOrder) => _blocks.Add(new ArticleBlock
    {
        ArticleId = Id,
        Kind = kind,
        Text = text,
        SortOrder = sortOrder,
    });

    public void ClearBlocks() => _blocks.Clear();
}

/// <summary>
/// The three block types the frontend's <c>ArticleBlock</c> union declares.
/// Serialised lowercase to match <c>'paragraph' | 'heading' | 'product'</c>.
/// </summary>
public enum ArticleBlockKind
{
    Paragraph,
    Heading,
    Product,
}

public sealed class ArticleBlock : Entity
{
    public required Guid ArticleId { get; init; }

    public required ArticleBlockKind Kind { get; init; }

    /// <summary>Null for a <see cref="ArticleBlockKind.Product"/> block — it has no text of its own.</summary>
    public string? Text { get; set; }

    public int SortOrder { get; set; }
}
