using Bojan.Domain.Common;

namespace Bojan.Domain.Business;

/// <summary>The frontend's <c>B2BRequest.kind</c> — <c>'organization' | 'bulk' | 'promotional'</c>.</summary>
public enum BusinessRequestKind
{
    Organization,
    Bulk,
    Promotional,
}

/// <summary>The frontend's <c>B2BRequestStatus</c> — screens 61-65.</summary>
public enum BusinessRequestStatus
{
    Submitted,
    Reviewing,
    Quoted,
    Approved,
    Rejected,
}

/// <summary>
/// A business enquiry — a quote request, a bulk order, or a promotional-gift
/// brief.
/// </summary>
/// <remarks>
/// Three public forms feed this one entity: <c>POST /business/requests</c>,
/// <c>POST /business/bulk-orders</c> and the organisation profile behind
/// <c>PUT /business/organization</c>. All three are allow-listed on the
/// frontend (<c>apps/storefront/src/app/api/account/[action]/route.ts</c>) and
/// only two of them require a session, so <see cref="CustomerId"/> is
/// nullable and the contact details travel with the row.
/// </remarks>
public sealed class BusinessRequest : Entity
{
    /// <summary>Human-facing code in the <c>B2B-0000</c> shape the request screens render.</summary>
    public required string Code { get; init; }

    public Guid? CustomerId { get; init; }

    public required BusinessRequestKind Kind { get; init; }

    public required string Title { get; set; }

    public required string Organization { get; set; }

    public required string ContactName { get; set; }

    public required string Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>How many units the enquiry covers — the <c>items</c> field on both forms.</summary>
    public int ItemCount { get; set; }

    public string? Description { get; set; }

    /// <summary>When the customer needs it by; free text, since the form takes a Jalali date the shopper types.</summary>
    public string? Deadline { get; set; }

    public BusinessRequestStatus Status { get; private set; } = BusinessRequestStatus.Submitted;

    /// <summary>Operator handling it — set by the panel's <c>business-requests</c> write.</summary>
    public Guid? AssigneeId { get; set; }

    /// <summary>Internal note from the panel; never surfaced to the customer.</summary>
    public string? InternalNote { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    private readonly List<BusinessRequestEvent> _timeline = [];
    public IReadOnlyCollection<BusinessRequestEvent> Timeline => _timeline;

    private BusinessRequest()
    {
    }

    public static BusinessRequest Create(
        string code,
        BusinessRequestKind kind,
        string title,
        string organization,
        string contactName,
        string phone,
        DateTimeOffset nowUtc,
        Guid? customerId = null,
        string? email = null,
        int itemCount = 0,
        string? description = null,
        string? deadline = null)
    {
        var request = new BusinessRequest
        {
            Code = code,
            Kind = kind,
            Title = title,
            Organization = organization,
            ContactName = contactName,
            Phone = phone,
            CustomerId = customerId,
            Email = email,
            ItemCount = itemCount,
            Description = description,
            Deadline = deadline,
            CreatedAtUtc = nowUtc,
        };

        request._timeline.Add(BusinessRequestEvent.For(request.Id, BusinessRequestStatus.Submitted, nowUtc));
        return request;
    }

    /// <returns>The timeline entry this appended — see <c>Order.TransitionTo</c> for why the caller needs it.</returns>
    public BusinessRequestEvent TransitionTo(BusinessRequestStatus next, DateTimeOffset nowUtc)
    {
        if (Status is BusinessRequestStatus.Rejected)
        {
            throw new InvalidOperationException($"Business request {Code} was rejected and cannot transition further.");
        }

        Status = next;
        var entry = BusinessRequestEvent.For(Id, next, nowUtc);
        _timeline.Add(entry);
        return entry;
    }
}

public sealed class BusinessRequestEvent : Entity
{
    public required Guid BusinessRequestId { get; init; }

    public required BusinessRequestStatus Status { get; init; }

    public required DateTimeOffset AtUtc { get; init; }

    public static BusinessRequestEvent For(Guid requestId, BusinessRequestStatus status, DateTimeOffset nowUtc) => new()
    {
        BusinessRequestId = requestId,
        Status = status,
        AtUtc = nowUtc,
    };
}

/// <summary>
/// A registered business customer — the profile behind
/// <c>PUT /business/organization</c>, screen 68.
/// </summary>
public sealed class BusinessOrganization : Entity
{
    public required Guid CustomerId { get; init; }

    public required string Name { get; set; }

    public string? RegistrationNumber { get; set; }

    /// <summary>Iranian economic code (کد اقتصادی) — needed for a formal invoice.</summary>
    public string? EconomicCode { get; set; }

    public string? Province { get; set; }

    public string? City { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
