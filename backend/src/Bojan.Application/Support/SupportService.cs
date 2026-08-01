using Bojan.Application.Common;
using Bojan.Domain.Catalogue;
using Bojan.Domain.Support;

namespace Bojan.Application.Support;

/// <summary>Fields of <c>POST /support/messages</c> — the contact form on screen 47.</summary>
public sealed record ContactMessageRequest(string Name, string? Phone, string? Email, string Subject, string Body);

/// <summary>Fields of <c>POST /stock-alerts</c> — screen 87.</summary>
public sealed record StockAlertRequest(string ProductSlug, string? Phone, string? Email);

/// <summary>
/// The public support writes.
/// </summary>
/// <remarks>
/// Both are allow-listed <c>private: false</c> on the frontend, so neither
/// requires a session. A signed-in customer's id is passed when there is one,
/// purely so the resulting ticket shows up in their own list.
/// </remarks>
public sealed class SupportService(
    ISupportRepository tickets,
    IStockAlertRepository alerts,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<Guid> SubmitContactMessageAsync(
        Guid? customerId,
        ContactMessageRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = new SupportTicket
        {
            CustomerId = customerId,
            ContactName = request.Name,
            ContactPhone = request.Phone,
            ContactEmail = request.Email,
            Subject = request.Subject,
            CreatedAtUtc = clock.UtcNow,
        };

        // The form's body is the thread's first message rather than a field on
        // the ticket, so an operator's reply continues one conversation instead
        // of starting a second.
        ticket.AddMessage(request.Body, fromSupport: false, clock.UtcNow);

        tickets.AddTicket(ticket);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ticket.Id;
    }

    public async Task<UseCaseResult> RequestStockAlertAsync(
        StockAlertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) && string.IsNullOrWhiteSpace(request.Email))
        {
            // Nowhere to send the alert to.
            return UseCaseResult.Failure(UseCaseError.Invalid, "contact");
        }

        var productId = await alerts.FindProductIdBySlugAsync(request.ProductSlug, cancellationToken);
        if (productId is null)
        {
            return UseCaseResult.Failure(UseCaseError.NotFound);
        }

        // Asking twice is not an error, it is the same request — and it must
        // not queue a second SMS for the same restock.
        if (await alerts.ExistsAsync(productId.Value, request.Phone, request.Email, cancellationToken))
        {
            return UseCaseResult.Success();
        }

        alerts.Add(new StockAlert
        {
            ProductId = productId.Value,
            Phone = request.Phone,
            Email = request.Email,
            CreatedAtUtc = clock.UtcNow,
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }
}
