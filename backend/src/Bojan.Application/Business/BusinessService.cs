using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Business;
using Bojan.Domain.Orders;

namespace Bojan.Application.Business;

/// <summary>Fields of <c>POST /business/requests</c> — the quote form on screen 62.</summary>
public sealed record CreateBusinessRequest(
    string Organization,
    string Contact,
    string Phone,
    string? Email,
    string? Items,
    string? Description,
    string? Deadline);

/// <summary>Fields of <c>POST /business/bulk-orders</c> — screen 67.</summary>
public sealed record CreateBulkOrderRequest(
    string Organization,
    string Contact,
    string Phone,
    string? Email,
    string? Items,
    string? Note);

/// <summary>Fields of <c>PUT /business/organization</c> — screen 68.</summary>
public sealed record SaveOrganizationRequest(
    string Organization,
    string? RegistrationNumber,
    string? EconomicCode,
    string? Province,
    string? City,
    string? Address,
    string? Phone,
    string? Email);

/// <summary>
/// The B2B writes.
/// </summary>
/// <remarks>
/// Two of the three are public: the quote and bulk-order forms are allow-listed
/// <c>private: false</c> on the frontend, so a visitor with no account can send
/// one. <paramref name="customerId"/> being null is therefore normal, not an
/// error — it only means the request will not appear in anyone's account
/// history, just in the panel.
/// </remarks>
public sealed class BusinessService(
    IBusinessRepository repository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<B2BRequestDto> CreateRequestAsync(
        Guid? customerId,
        CreateBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var created = BusinessRequest.Create(
            OrderNumber.NewBusinessRequestCode(),
            BusinessRequestKind.Organization,
            TitleFor(request.Organization, request.Description),
            request.Organization,
            request.Contact,
            request.Phone,
            clock.UtcNow,
            customerId,
            request.Email,
            ParseItemCount(request.Items),
            request.Description,
            request.Deadline);

        repository.AddRequest(created);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(created, quoteId: null);
    }

    public async Task<B2BRequestDto> CreateBulkOrderAsync(
        Guid? customerId,
        CreateBulkOrderRequest request,
        CancellationToken cancellationToken)
    {
        var created = BusinessRequest.Create(
            OrderNumber.NewBusinessRequestCode(),
            BusinessRequestKind.Bulk,
            TitleFor(request.Organization, request.Note),
            request.Organization,
            request.Contact,
            request.Phone,
            clock.UtcNow,
            customerId,
            request.Email,
            ParseItemCount(request.Items),
            request.Note);

        repository.AddRequest(created);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(created, quoteId: null);
    }

    public async Task<UseCaseResult> SaveOrganizationAsync(
        Guid customerId,
        SaveOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var organization = await repository.FindOrganizationAsync(customerId, cancellationToken);

        if (organization is null)
        {
            organization = new BusinessOrganization
            {
                CustomerId = customerId,
                Name = request.Organization,
            };
            repository.AddOrganization(organization);
        }
        else
        {
            organization.Name = request.Organization;
        }

        organization.RegistrationNumber = request.RegistrationNumber;
        organization.EconomicCode = request.EconomicCode;
        organization.Province = request.Province;
        organization.City = request.City;
        organization.Address = request.Address;
        organization.Phone = request.Phone;
        organization.Email = request.Email;
        organization.UpdatedAtUtc = clock.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// The forms take "how many" as free text ("حدود ۵۰ عدد"), so a number is
    /// extracted where one exists and zero stands for "not stated" — refusing
    /// the submission over an unparseable count would lose a lead over
    /// formatting.
    /// </summary>
    /// <remarks>
    /// Persian and Arabic-Indic digits are normalised to ASCII first. A shopper
    /// on a Persian keyboard types ۵۰, and the frontend only normalises the
    /// fields it validates itself — this one it forwards as typed.
    /// </remarks>
    private static int ParseItemCount(string? items)
    {
        if (string.IsNullOrWhiteSpace(items))
        {
            return 0;
        }

        var digits = new string([.. items.Select(NormaliseDigit).Where(char.IsAsciiDigit)]);
        return digits.Length > 0 && long.TryParse(digits, out var parsed)
            ? (int)Math.Min(parsed, int.MaxValue)
            : 0;
    }

    private static char NormaliseDigit(char character) => character switch
    {
        // Persian ۰-۹.
        >= '۰' and <= '۹' => (char)(character - '۰' + '0'),
        // Arabic-Indic ٠-٩, which some keyboards produce instead.
        >= '٠' and <= '٩' => (char)(character - '٠' + '0'),
        _ => character,
    };

    private static string TitleFor(string organization, string? detail)
    {
        var trimmed = detail?.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? organization
            : trimmed.Length <= 60 ? trimmed : trimmed[..60].TrimEnd() + "…";
    }

    private static B2BRequestDto ToDto(BusinessRequest request, string? quoteId) => new(
        request.Id.ToString(),
        request.Code,
        request.Title,
        WireFormat.BusinessRequestKind(request.Kind),
        WireFormat.BusinessRequestStatus(request.Status),
        request.CreatedAtUtc,
        request.Organization,
        request.ItemCount,
        quoteId,
        Timelines.ForBusinessRequest(
            request.Status,
            request.Timeline.ToDictionary(step => step.Status, step => step.AtUtc)));
}
