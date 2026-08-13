using Bojan.Application.Common;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;

namespace Bojan.Application.Administration;

/// <summary>
/// The magazine's articles, from the panel.
/// </summary>
/// <remarks>
/// <para>
/// The panel had an article editor and it wrote to the wrong table. «مدیریت
/// محتوا» saves a <c>ContentEntry</c> with <c>kind: article</c>; the magazine
/// reads <c>Article</c>, a different table with its own columns — category,
/// reading time, featured flag, and a body of typed blocks the article page
/// renders. The two were never related. So an article published from the panel
/// appeared nowhere on the site, and every article on the site was invisible to
/// the panel, because the seeder had written those into <c>Article</c>.
/// </para>
/// <para>
/// This service is the missing half: the panel's article screens write the
/// table the storefront serves. <c>ContentEntry</c> keeps the other three kinds
/// — static page, banner, FAQ — which is what it was always right for.
/// </para>
/// </remarks>
public sealed class AdminArticleService(
    IAdminRepository repository,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    IDateTimeProvider clock)
{
    /// <summary>
    /// Roughly how long the body takes to read, in minutes.
    /// </summary>
    /// <remarks>
    /// Two hundred words a minute is the usual figure for prose, and the card
    /// shows it. Derived rather than typed because it is a fact about the text:
    /// a field would be another thing to keep in step with an edit, and it
    /// would be wrong the first time somebody forgot.
    /// </remarks>
    private const int WordsPerMinute = 200;

    public async Task<UseCaseResult<string>> SaveAsync(
        SaveArticleRequest request,
        CancellationToken cancellationToken)
    {
        Article article;

        if (Guid.TryParse(request.Id, out var id))
        {
            if (await repository.FindArticleAsync(id, cancellationToken) is not { } existing)
            {
                return UseCaseResult<string>.Failure(UseCaseError.NotFound);
            }

            article = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "title");
            }

            /*
                `Slug.From` throws when nothing in the value can become a slug —
                a title of "!!!", or text that arrived mangled somewhere
                upstream. Thrown out of here it is an unhandled exception and a
                500, which tells the operator that the shop broke rather than
                that the title cannot be used. It is a field error.
            */
            var source = string.IsNullOrWhiteSpace(request.Slug) ? request.Title : request.Slug;
            if (TryMakeSlug(source) is not { } generated)
            {
                return UseCaseResult<string>.Failure(
                    UseCaseError.Invalid,
                    string.IsNullOrWhiteSpace(request.Slug) ? "title" : "slug");
            }

            if (await repository.IsArticleSlugTakenAsync(generated, Guid.Empty, cancellationToken))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Conflict, "slug");
            }

            article = new Article
            {
                Title = request.Title.Trim(),
                Slug = generated,
                PublishedAtUtc = clock.UtcNow,
            };

            repository.AddArticle(article);
        }

        if (request.Title is not null)
        {
            if (request.Title.Trim().Length == 0)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "title");
            }

            article.Title = request.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            // Normalised the same way a generated one is, so a hand-typed slug
            // cannot produce an address the storefront's routes will not match.
            if (TryMakeSlug(request.Slug) is not { } slug)
            {
                return UseCaseResult<string>.Failure(UseCaseError.Invalid, "slug");
            }

            if (slug != article.Slug
                && await repository.IsArticleSlugTakenAsync(slug, article.Id, cancellationToken))
            {
                return UseCaseResult<string>.Failure(UseCaseError.Conflict, "slug");
            }

            article.Slug = slug;
        }

        if (request.Excerpt is not null) article.Excerpt = request.Excerpt.Trim();
        if (request.Category is not null) article.Category = request.Category.Trim();
        if (request.Featured is { } featured) article.IsFeatured = featured;

        if (request.Cover is not null)
        {
            article.CoverUrl = request.Cover.Trim();
        }

        if (request.Body is not null)
        {
            ApplyBody(article, request.Body);
        }

        if (request.Status is not null)
        {
            switch (request.Status)
            {
                case "archived":
                    article.SoftDelete(clock.UtcNow);
                    break;
                case "published":
                    article.Restore();
                    // The date the readers see is the date it actually went
                    // out, not the date it was first drafted — an article
                    // written in Farvardin and published in Mordad belongs at
                    // the top of the list, not buried four months down.
                    if (!article.IsPublished) article.PublishedAtUtc = clock.UtcNow;
                    article.IsPublished = true;
                    break;
                case "draft":
                    article.Restore();
                    article.IsPublished = false;
                    break;
                default:
                    return UseCaseResult<string>.Failure(UseCaseError.Invalid, "status");
            }
        }

        audit.Record("article.saved", article.Slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult<string>.Success(article.Id.ToString());
    }

    /// <summary>
    /// A slug for this value, or null when it has none to give.
    /// </summary>
    /// <remarks>
    /// <see cref="Slug.From"/> throws for a value with nothing sluggable in it,
    /// which is the right shape for a domain invariant and the wrong shape for
    /// an operator typing into a form: unhandled, it is a 500 that says the
    /// shop broke. Persian titles slug perfectly well — the generator keeps the
    /// script rather than romanising it — so the values that land here are
    /// punctuation-only titles and text that arrived mangled from somewhere
    /// upstream, and both are field errors.
    /// </remarks>
    private static string? TryMakeSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        try
        {
            return Slug.From(value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Turns the editor's plain text into the blocks the article page renders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The storefront renders a list of typed blocks, not a blob — so something
    /// has to translate, and the alternative to this is a block editor, which is
    /// a great deal of screen for a shop that writes an article a month.
    /// </para>
    /// <para>
    /// A blank line separates paragraphs, and a line beginning <c>##</c> is a
    /// heading. Two rules, both of which somebody typing into a textarea will
    /// guess or read once and remember — and the hint under the field says so.
    /// </para>
    /// </remarks>
    private static void ApplyBody(Article article, string body)
    {
        article.ClearBlocks();

        var order = 0;
        var paragraphs = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.StartsWith("##", StringComparison.Ordinal))
            {
                article.AddBlock(ArticleBlockKind.Heading, paragraph.TrimStart('#').Trim(), order++);
                continue;
            }

            article.AddBlock(ArticleBlockKind.Paragraph, paragraph, order++);
        }

        article.ReadingMinutes = Math.Max(
            1,
            (int)Math.Ceiling(
                body.Split(' ', '\n', '\t').Count(word => word.Length > 0) / (double)WordsPerMinute));
    }
}
