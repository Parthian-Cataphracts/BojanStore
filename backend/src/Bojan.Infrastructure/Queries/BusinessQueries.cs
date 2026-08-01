using Bojan.Application.Business;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Business;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Queries;

/// <summary>Phase 5's B2B reads — screens 61-70.</summary>
public sealed class BusinessQueries(BojanDbContext db) : IBusinessQueries
{
    public async Task<IReadOnlyList<B2BRequestDto>> ListRequestsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var requests = await db.BusinessRequests.AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .Include(r => r.Timeline)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return await WithQuoteIdsAsync(requests, cancellationToken);
    }

    public async Task<B2BRequestDto?> GetRequestAsync(Guid customerId, string idOrCode, CancellationToken cancellationToken)
    {
        var query = db.BusinessRequests.AsNoTracking().Where(r => r.CustomerId == customerId);

        query = Guid.TryParse(idOrCode, out var id)
            ? query.Where(r => r.Id == id)
            : query.Where(r => r.Code == idOrCode);

        var request = await query.Include(r => r.Timeline).FirstOrDefaultAsync(cancellationToken);
        if (request is null)
        {
            return null;
        }

        return (await WithQuoteIdsAsync([request], cancellationToken)).FirstOrDefault();
    }

    /// <summary>
    /// Attaches the quote id each request has, if any, in one query rather than
    /// one per row — <c>quoteId</c> is what screen 63's "view quote" link needs.
    /// </summary>
    private async Task<IReadOnlyList<B2BRequestDto>> WithQuoteIdsAsync(
        IReadOnlyList<BusinessRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return [];
        }

        var ids = requests.Select(r => r.Id).ToList();

        var quotes = await db.Quotes.AsNoTracking()
            .Where(q => ids.Contains(q.BusinessRequestId))
            .Select(q => new { q.BusinessRequestId, q.Id })
            .ToListAsync(cancellationToken);

        return [.. requests.Select(request => new B2BRequestDto(
            request.Id.ToString(),
            request.Code,
            request.Title,
            WireFormat.BusinessRequestKind(request.Kind),
            WireFormat.BusinessRequestStatus(request.Status),
            request.CreatedAtUtc,
            request.Organization,
            request.ItemCount,
            quotes.FirstOrDefault(q => q.BusinessRequestId == request.Id)?.Id.ToString(),
            Timelines.ForBusinessRequest(
                request.Status,
                request.Timeline
                    .GroupBy(step => step.Status)
                    .ToDictionary(group => group.Key, group => group.Min(step => step.AtUtc)))))];
    }

    public async Task<IReadOnlyList<QuoteDto>> ListQuotesAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var quotes = await OwnedQuotes(customerId)
            .Include(q => q.Lines)
            .OrderByDescending(q => q.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return [.. quotes.Select(ToDto)];
    }

    public async Task<QuoteDto?> GetQuoteAsync(Guid customerId, string idOrNumber, CancellationToken cancellationToken)
    {
        var query = OwnedQuotes(customerId);

        query = Guid.TryParse(idOrNumber, out var id)
            ? query.Where(q => q.Id == id)
            : query.Where(q => q.Number == idOrNumber);

        var quote = await query.Include(q => q.Lines).FirstOrDefaultAsync(cancellationToken);
        return quote is null ? null : ToDto(quote);
    }

    /// <summary>
    /// A quote belongs to a customer through the request it answers, so
    /// ownership is checked on that join rather than on the quote itself.
    /// </summary>
    private IQueryable<Quote> OwnedQuotes(Guid customerId) =>
        db.Quotes.AsNoTracking()
            .Where(q => db.BusinessRequests.Any(r => r.Id == q.BusinessRequestId && r.CustomerId == customerId));

    private static QuoteDto ToDto(Quote quote) => new(
        quote.Id.ToString(),
        quote.Number,
        quote.RequestCode,
        quote.Organization,
        quote.SalesRep,
        quote.ValidUntilUtc,
        WireFormat.QuoteStatus(quote.Status),
        [.. quote.Lines.Select(l => new QuoteLineDto(l.Title, l.Sku, l.Quantity, l.UnitPrice.Amount))],
        quote.Subtotal.Amount,
        quote.Discount.Amount,
        quote.Tax.Amount,
        quote.Total.Amount);

    public async Task<IReadOnlyList<GiftBundleDto>> ListGiftBundlesAsync(string? category, CancellationToken cancellationToken)
    {
        var query = db.GiftBundles.AsNoTracking().Where(b => b.IsPublished);

        // "همه بسته‌ها" is the frontend's own all-categories chip, not a
        // category — it reaches here only if a caller forwards it verbatim.
        if (!string.IsNullOrWhiteSpace(category) && category != "همه بسته‌ها")
        {
            query = query.Where(b => b.Category == category);
        }

        return await query
            .OrderBy(b => b.Title)
            .Select(b => new GiftBundleDto(
                b.Slug, b.Title, b.Summary, b.CoverUrl, b.Category, b.PricePerUnit.Amount, b.MinimumQuantity))
            .ToListAsync(cancellationToken);
    }
}
