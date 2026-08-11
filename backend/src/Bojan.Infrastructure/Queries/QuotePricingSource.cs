using Bojan.Application.Business;
using Bojan.Domain.Catalogue;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Queries;

/// <summary>
/// What a quote needs to know about the products it names.
/// </summary>
/// <remarks>
/// <para>
/// The volume ladder is loaded with the price in one query rather than per line,
/// because a rep quoting forty products would otherwise make forty round trips
/// while an organisation waits.
/// </para>
/// <para>
/// Archived products are deliberately included. A quote is often issued against
/// an enquiry that arrived weeks earlier, and a product withdrawn from the
/// storefront in the meantime is still a thing the shop can sell to an
/// organisation by arrangement — refusing to price it would turn an operator's
/// judgement into an error message. Whether it should be on the quote at all is
/// the rep's call, and they are looking at the name while they add it.
/// </para>
/// </remarks>
public sealed class QuotePricingSource(BojanDbContext db) : IQuotePricingSource
{
    public async Task<IReadOnlyDictionary<Guid, QuotableProduct>> LoadForQuotingAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var products = await db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new
            {
                product.Id,
                product.Title,
                product.Sku,
                product.Price,
            })
            .ToListAsync(cancellationToken);

        var tiers = await db.ProductVolumeTiers
            .AsNoTracking()
            .Where(tier => productIds.Contains(tier.ProductId))
            .ToListAsync(cancellationToken);

        var byProduct = tiers
            .GroupBy(tier => tier.ProductId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProductVolumeTier>)[.. group.OrderBy(tier => tier.MinimumQuantity)]);

        return products.ToDictionary(
            product => product.Id,
            product => new QuotableProduct(
                product.Id,
                product.Title,
                product.Sku,
                product.Price,
                byProduct.TryGetValue(product.Id, out var ladder) ? ladder : []));
    }
}
