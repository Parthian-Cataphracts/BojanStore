using Bojan.Domain.Common;

namespace Bojan.Domain.Reviews;

/// <summary>
/// Moderation state shared by reviews and questions.
/// </summary>
/// <remarks>
/// <c>BACKEND.md</c> Phase 5: "Reviews and questions need a moderation state —
/// the panel has screens for it." The three values match the storefront's
/// <c>ReviewStatus</c> (<c>'published' | 'pending' | 'rejected'</c>) so the
/// customer's own list on screen 56 can render this field directly.
/// </remarks>
public enum ModerationStatus
{
    Pending,
    Published,
    Rejected,
}

/// <summary>
/// A customer's review of a product — screen 84 shows them, screen 56 shows
/// the author their own.
/// </summary>
/// <remarks>
/// <see cref="AuthorName"/> is captured at write time rather than joined from
/// the customer: a review is published under the name the shopper had then,
/// and a later profile edit must not silently rewrite attributed text.
/// </remarks>
public sealed class ProductReview : Entity
{
    public required Guid ProductId { get; init; }

    public required Guid CustomerId { get; init; }

    public required string AuthorName { get; init; }

    /// <summary>One to five. Enforced by the application layer's validator, not by a database check.</summary>
    public required int Rating { get; set; }

    public string? Title { get; set; }

    public required string Body { get; set; }

    /// <summary>The "would you recommend it" toggle on the review form.</summary>
    public bool Recommend { get; set; }

    public ModerationStatus Status { get; set; } = ModerationStatus.Pending;

    /// <summary>True when the author actually bought the product — drives the badge on the review card.</summary>
    public bool IsVerifiedPurchase { get; init; }

    public int HelpfulCount { get; set; }

    /// <summary>
    /// Picked out by an operator for the «نظرات مشتریان» rail on the home page.
    /// </summary>
    /// <remarks>
    /// Curation, not a ranking: the home page shows a handful of reviews as a
    /// testimonial, and which handful is an editorial choice — the newest five
    /// or the five with the most «مفید بود» votes are both wrong on the day a
    /// three-star review is the newest. The flag is only ever honoured
    /// alongside <see cref="ModerationStatus.Published"/>, so unticking
    /// «تأیید شده» takes a review off the home page without anyone having to
    /// remember to untick this as well.
    /// </remarks>
    public bool IsFeaturedOnHome { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>A question asked on a product page — screen 85, with an optional staff answer.</summary>
public sealed class ProductQuestion : Entity
{
    public required Guid ProductId { get; init; }

    public required Guid CustomerId { get; init; }

    public required string AuthorName { get; init; }

    public required string Body { get; set; }

    public ModerationStatus Status { get; set; } = ModerationStatus.Pending;

    public DateTimeOffset AskedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? AnswerBody { get; set; }

    public string? AnswerAuthor { get; set; }

    public DateTimeOffset? AnsweredAtUtc { get; set; }

    public void Answer(string author, string body, DateTimeOffset nowUtc)
    {
        AnswerAuthor = author;
        AnswerBody = body;
        AnsweredAtUtc = nowUtc;
        Status = ModerationStatus.Published;
    }
}
