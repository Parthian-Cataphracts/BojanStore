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

    /// <summary>Records a status move on a request — the timeline screen 63 draws.</summary>
    void AddRequestEvent(BusinessRequestEvent request);

    /// <summary>
    /// Files the in-app notice that a quote exists.
    /// </summary>
    /// <remarks>
    /// Written inside the same change set as the quote, so an organisation
    /// cannot be told about a pro-forma that was not saved — or left uninformed
    /// about one that was.
    /// </remarks>
    void AddCustomerNotification(Domain.Customers.CustomerNotification notification);
}
