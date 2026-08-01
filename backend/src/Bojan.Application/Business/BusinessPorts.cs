using Bojan.Application.Contracts;
using Bojan.Domain.Business;

namespace Bojan.Application.Business;

/// <summary>Reads behind screens 61-70 — the customer's own B2B history.</summary>
public interface IBusinessQueries
{
    /// <summary>
    /// A customer's requests. Anonymous submissions (the public forms) belong
    /// to no one and never appear here — only in the panel.
    /// </summary>
    Task<IReadOnlyList<B2BRequestDto>> ListRequestsAsync(Guid customerId, CancellationToken cancellationToken);

    Task<B2BRequestDto?> GetRequestAsync(Guid customerId, string idOrCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<QuoteDto>> ListQuotesAsync(Guid customerId, CancellationToken cancellationToken);

    Task<QuoteDto?> GetQuoteAsync(Guid customerId, string idOrNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<GiftBundleDto>> ListGiftBundlesAsync(string? category, CancellationToken cancellationToken);
}

public interface IBusinessRepository
{
    void AddRequest(BusinessRequest request);

    Task<BusinessRequest?> FindRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<BusinessOrganization?> FindOrganizationAsync(Guid customerId, CancellationToken cancellationToken);

    void AddOrganization(BusinessOrganization organization);

    void AddQuote(Quote quote);
}
