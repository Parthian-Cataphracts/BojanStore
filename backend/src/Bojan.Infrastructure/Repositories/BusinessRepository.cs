using Bojan.Application.Business;
using Bojan.Application.Support;
using Bojan.Domain.Business;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Support;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Infrastructure.Repositories;

public sealed class BusinessRepository(BojanDbContext db) : IBusinessRepository
{
    public void AddRequest(BusinessRequest request) => db.BusinessRequests.Add(request);

    public Task<BusinessRequest?> FindRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        db.BusinessRequests.Include(r => r.Timeline).FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

    public Task<BusinessOrganization?> FindOrganizationAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.BusinessOrganizations.FirstOrDefaultAsync(o => o.CustomerId == customerId, cancellationToken);

    public void AddOrganization(BusinessOrganization organization) => db.BusinessOrganizations.Add(organization);

    public void AddQuote(Quote quote) => db.Quotes.Add(quote);
}

public sealed class SupportRepository(BojanDbContext db) : ISupportRepository
{
    public void AddTicket(SupportTicket ticket) => db.SupportTickets.Add(ticket);

    public Task<SupportTicket?> FindTicketAsync(Guid ticketId, CancellationToken cancellationToken) =>
        db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

    public Task<SupportTicket?> FindTicketWithMessagesAsync(Guid ticketId, CancellationToken cancellationToken) =>
        db.SupportTickets.Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

    public void AddMessage(SupportMessage message) => db.SupportMessages.Add(message);

    public Task<CannedReply?> FindCannedReplyAsync(Guid id, CancellationToken cancellationToken) =>
        db.CannedReplies.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public void AddCannedReply(CannedReply reply) => db.CannedReplies.Add(reply);
}

public sealed class StockAlertRepository(BojanDbContext db) : IStockAlertRepository
{
    public Task<Guid?> FindProductIdBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Products.AsNoTracking()
            .Where(p => p.Slug == slug)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Only alerts that have not fired yet count as duplicates: once a customer
    /// has been told, asking again about the next restock is a new request.
    /// </summary>
    public Task<bool> ExistsAsync(Guid productId, string? phone, string? email, CancellationToken cancellationToken) =>
        db.StockAlerts.AnyAsync(
            a => a.ProductId == productId
                && a.NotifiedAtUtc == null
                && ((phone != null && a.Phone == phone) || (email != null && a.Email == email)),
            cancellationToken);

    /// <summary>
    /// Tracked, not <c>AsNoTracking</c>: the caller stamps
    /// <c>NotifiedAtUtc</c> on each one it mails, and that write is what stops
    /// the next restock telling the same person again.
    /// </summary>
    public async Task<IReadOnlyList<StockAlert>> ListPendingAsync(
        Guid productId,
        CancellationToken cancellationToken) =>
        await db.StockAlerts
            .Where(a => a.ProductId == productId && a.NotifiedAtUtc == null && a.Email != null)
            .ToListAsync(cancellationToken);

    public void Add(StockAlert alert) => db.StockAlerts.Add(alert);
}
