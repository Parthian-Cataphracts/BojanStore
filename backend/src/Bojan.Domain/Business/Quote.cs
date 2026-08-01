using Bojan.Domain.Common;

namespace Bojan.Domain.Business;

/// <summary>The frontend's <c>Quote.status</c> — <c>'pending' | 'approved' | 'expired'</c>.</summary>
public enum QuoteStatus
{
    Pending,
    Approved,
    Expired,
}

/// <summary>
/// A priced answer to a <see cref="BusinessRequest"/> — screens 64 and 65.
/// </summary>
/// <remarks>
/// <see cref="Subtotal"/> and <see cref="Total"/> are derived from the lines
/// rather than stored, mirroring the fixture's own comment ("derive money so
/// the lines and the totals can never disagree"). <see cref="Discount"/> and
/// <see cref="TaxRatePercent"/> are the two inputs a sales rep actually sets.
/// </remarks>
public sealed class Quote : Entity
{
    /// <summary>Human-facing number in the <c>QT-0000-0000</c> shape screen 65 renders.</summary>
    public required string Number { get; init; }

    public required Guid BusinessRequestId { get; init; }

    public required string RequestCode { get; init; }

    public required string Organization { get; set; }

    public required string SalesRep { get; set; }

    public required DateTimeOffset ValidUntilUtc { get; set; }

    public QuoteStatus Status { get; set; } = QuoteStatus.Pending;

    public Money Discount { get; set; } = Money.Zero;

    /// <summary>Iranian VAT, a whole percent. Applied to the discounted subtotal.</summary>
    public int TaxRatePercent { get; set; } = 9;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    private readonly List<QuoteLine> _lines = [];
    public IReadOnlyCollection<QuoteLine> Lines => _lines;

    public Money Subtotal => _lines.Aggregate(Money.Zero, (sum, line) => sum + line.UnitPrice * line.Quantity);

    public Money Tax => new(Subtotal.ClampedMinus(Discount).Amount * TaxRatePercent / 100);

    public Money Total => Subtotal.ClampedMinus(Discount) + Tax;

    public void AddLine(string title, string sku, int quantity, Money unitPrice) => _lines.Add(new QuoteLine
    {
        QuoteId = Id,
        Title = title,
        Sku = sku,
        Quantity = quantity,
        UnitPrice = unitPrice,
    });
}

public sealed class QuoteLine : Entity
{
    public required Guid QuoteId { get; init; }

    public required string Title { get; init; }

    public required string Sku { get; init; }

    public required int Quantity { get; init; }

    public required Money UnitPrice { get; init; }
}

/// <summary>A corporate gift bundle offered on screen 66.</summary>
public sealed class GiftBundle : SoftDeletableEntity
{
    public required string Slug { get; set; }

    public required string Title { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string CoverUrl { get; set; } = string.Empty;

    /// <summary>Display grouping ("مینیمال", "هنری", "مدیریتی") — the filter chips on screen 66.</summary>
    public string Category { get; set; } = string.Empty;

    public required Money PricePerUnit { get; set; }

    public int MinimumQuantity { get; set; } = 1;

    public bool IsPublished { get; set; } = true;
}
