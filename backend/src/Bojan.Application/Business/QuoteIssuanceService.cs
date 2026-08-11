using Bojan.Application.Administration;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Notifications;
using Bojan.Domain.Business;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Common;
using Bojan.Domain.Customers;
using Bojan.Domain.Orders;

namespace Bojan.Application.Business;

/// <summary>
/// Issues a pro-forma invoice against a business request.
/// </summary>
/// <remarks>
/// <para>
/// The operation the B2B half was missing. Organisations could submit a request
/// and operators could move it through its statuses, but nothing anywhere could
/// turn one into a priced answer — the <see cref="Quote"/> aggregate, its lines,
/// the customer's screens for reading one and the "پیش‌فاکتور آماده شد" email
/// were all built and all waiting on this.
/// </para>
/// <para>
/// <b>Prices are not taken from the request.</b> The operator names products and
/// quantities; every unit price comes from the catalogue and every discount from
/// the product's own volume ladder. A rep may override a line, and that is a
/// deliberate act on a figure the system had already computed rather than a
/// blank box they have to fill correctly.
/// </para>
/// </remarks>
public sealed class QuoteIssuanceService(
    IBusinessRepository repository,
    IQuotePricingSource catalogue,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    ICustomerMailer mailer,
    EmailTemplates templates,
    IDateTimeProvider clock)
{
    /// <summary>How long a quote stands when the operator does not say.</summary>
    private static readonly TimeSpan DefaultValidity = TimeSpan.FromDays(14);

    /// <summary>Lines per quote. Past this it is a contract, not a pro-forma.</summary>
    private const int MaxLines = 100;

    public async Task<UseCaseResult<string>> IssueAsync(
        Guid actorId,
        string salesRep,
        IssueQuoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.RequestId, out var requestId))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "requestId");
        }

        if (request.Lines.Count is 0 or > MaxLines)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "lines");
        }

        if (request.Lines.Any(line => line.Quantity is < 1 or > 1_000_000))
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "quantity");
        }

        if (request.TaxRatePercent is < 0 or > 100)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "taxRatePercent");
        }

        var businessRequest = await repository.FindRequestAsync(requestId, cancellationToken);
        if (businessRequest is null)
        {
            return UseCaseResult<string>.Failure(UseCaseError.NotFound);
        }

        var productIds = request.Lines.Select(line => line.ProductId).Distinct().ToList();
        var priced = await catalogue.LoadForQuotingAsync(productIds, cancellationToken);

        // Every named product has to exist. A quote that silently drops a line
        // is a quote for something other than what was asked for.
        if (priced.Count != productIds.Count)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Invalid, "product");
        }

        var quote = new Quote
        {
            Number = OrderNumber.NewQuoteNumber(),
            BusinessRequestId = businessRequest.Id,
            RequestCode = businessRequest.Code,
            Organization = businessRequest.Organization,
            SalesRep = salesRep,
            ValidUntilUtc = request.ValidUntilUtc ?? clock.UtcNow + DefaultValidity,
            Discount = new Money(Math.Max(0, request.Discount)),
            TaxRatePercent = request.TaxRatePercent,
            CreatedAtUtc = clock.UtcNow,
        };

        foreach (var line in request.Lines)
        {
            var product = priced[line.ProductId];

            // The ladder decides, unless the rep named a figure. An override is
            // stored as the unit price rather than as a second discount, so the
            // document says what will be charged and nothing has to be
            // recomputed to read it.
            var unitPrice = line.UnitPriceOverride is { } overridden and >= 0
                ? new Money(overridden)
                : VolumePricing.UnitPriceFor(product.ListPrice, product.Tiers, line.Quantity);

            quote.AddLine(product.Title, product.Sku, line.Quantity, unitPrice);
        }

        repository.AddQuote(quote);

        // The request moves with it: a priced answer exists, so it is no longer
        // waiting on the shop. Failing here would be refusing to record a quote
        // that has already been built, so a status that will not advance is
        // reported rather than thrown.
        try
        {
            repository.AddRequestEvent(businessRequest.TransitionTo(BusinessRequestStatus.Quoted, clock.UtcNow));
        }
        catch (InvalidOperationException)
        {
            return UseCaseResult<string>.Failure(UseCaseError.Conflict, "terminal-status");
        }

        if (businessRequest.CustomerId is { } customerId)
        {
            repository.AddCustomerNotification(new CustomerNotification
            {
                CustomerId = customerId,
                Kind = NotificationKind.Business,
                Title = $"پیش‌فاکتور {quote.Number}",
                Body = $"برای درخواست {businessRequest.Code} پیش‌فاکتور صادر شد.",
                CreatedAtUtc = clock.UtcNow,
            }.WithLink($"/business/quotes/{quote.Id}"));
        }

        audit.Record("quote.issued", quote.Number);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // After the commit, and never allowed to fail the request: the quote is
        // real whether or not the mail server is reachable — see ICustomerMailer.
        await mailer.SendAsync(
            businessRequest.Email,
            templates.QuoteIssued(
                quote.Number,
                quote.Id,
                businessRequest.Code,
                quote.Total.Amount,
                quote.ValidUntilUtc),
            cancellationToken);

        return quote.Id.ToString();
    }
}

/// <summary>
/// What quoting needs to know about a product.
/// </summary>
/// <remarks>
/// Its own port rather than a reach into the catalogue's read model, because
/// this is the one caller that needs the volume ladder alongside the price, and
/// nothing else has any business loading it.
/// </remarks>
public interface IQuotePricingSource
{
    Task<IReadOnlyDictionary<Guid, QuotableProduct>> LoadForQuotingAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);
}

/// <summary>One product as a quote sees it: what it costs, and what volume does to that.</summary>
public sealed record QuotableProduct(
    Guid Id,
    string Title,
    string Sku,
    Money ListPrice,
    IReadOnlyList<ProductVolumeTier> Tiers);
