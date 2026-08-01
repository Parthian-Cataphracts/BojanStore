using FluentValidation;

namespace Bojan.Api.Contracts;

/// <summary>
/// Request bodies for the storefront's writes.
/// </summary>
/// <remarks>
/// <para>
/// Each record carries exactly the fields the frontend's allow-list forwards
/// (<c>apps/storefront/src/app/api/account/[action]/route.ts</c>) and nothing
/// else. That list already drops everything unrecognised; declaring the same
/// fields here is the second half of the guarantee, for a caller who skips the
/// proxy and posts straight to this API.
/// </para>
/// <para>
/// Validation messages are English and terse. <c>BACKEND.md</c> section 1.4:
/// the frontend owns the Persian copy, and every one of these forms already has
/// its own for the field it validates.
/// </para>
/// </remarks>
public sealed record UpdateProfileBody(
    string? FirstName,
    string? LastName,
    string? Email,
    string? BirthDate,
    string? City,
    string? NationalId,
    string? Avatar = null);

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileBody>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.City).MaximumLength(100);
        // Ten digits, the length of an Iranian national id. Its check digit is
        // not verified: a customer mistyping it should not be told their own
        // identity number is wrong by a shop.
        RuleFor(x => x.NationalId).Matches(@"^\d{10}$").When(x => !string.IsNullOrEmpty(x.NationalId));
        // Bounded here; whether it is a URL this API actually issued is decided
        // in AccountService, which is the only layer that knows where uploads
        // live.
        RuleFor(x => x.Avatar).MaximumLength(1000);
    }
}

public sealed record SaveAddressBody(
    string? Id,
    string Title,
    string Recipient,
    string Phone,
    string Province,
    string City,
    string PostalCode,
    string Line,
    bool? IsDefault);

public sealed class SaveAddressValidator : AbstractValidator<SaveAddressBody>
{
    public SaveAddressValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Recipient).NotEmpty().MaximumLength(150);
        // Accepts a mobile or a landline: the recipient may be a workplace.
        RuleFor(x => x.Phone).Matches(@"^0\d{9,10}$");
        RuleFor(x => x.Province).NotEmpty().MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode).Matches(@"^\d{10}$");
        RuleFor(x => x.Line).NotEmpty().MaximumLength(500);
    }
}

public sealed record IdBody(string Id);

public sealed record IdsBody(IReadOnlyList<string>? Ids);

public sealed record ProductIdBody(string ProductId);

public sealed record ReturnItemBody(string ProductId, int Quantity);

public sealed record CreateReturnBody(
    string OrderId,
    IReadOnlyList<ReturnItemBody> Items,
    string Reason,
    string? Description,
    string? RefundMethod);

public sealed class CreateReturnValidator : AbstractValidator<CreateReturnBody>
{
    public CreateReturnValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item => item.RuleFor(i => i.Quantity).InclusiveBetween(1, 100));
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed record CreateReviewBody(string ProductSlug, int Rating, string? Title, string Body, bool? Recommend);

public sealed class CreateReviewValidator : AbstractValidator<CreateReviewBody>
{
    public CreateReviewValidator()
    {
        RuleFor(x => x.ProductSlug).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Title).MaximumLength(300);
        // 2000 is the proxy's own per-field cap, repeated so a direct caller
        // meets the same ceiling.
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
    }
}

public sealed record CreateQuestionBody(string ProductSlug, string Body);

public sealed class CreateQuestionValidator : AbstractValidator<CreateQuestionBody>
{
    public CreateQuestionValidator()
    {
        RuleFor(x => x.ProductSlug).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
    }
}

public sealed record StockAlertBody(string ProductSlug, string? Phone, string? Email);

public sealed class StockAlertValidator : AbstractValidator<StockAlertBody>
{
    public StockAlertValidator()
    {
        RuleFor(x => x.ProductSlug).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).Matches(@"^09\d{9}$").When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Email));
        // One of the two has to be there or the alert has nowhere to go. The
        // service checks this too; here it becomes a field error the form can
        // point at rather than a bare 400.
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Phone) || !string.IsNullOrWhiteSpace(x.Email))
            .WithName("contact")
            .WithMessage("Either a phone number or an email address is required.");
    }
}

public sealed record ContactMessageBody(string Name, string? Phone, string? Email, string Subject, string Body);

public sealed class ContactMessageValidator : AbstractValidator<ContactMessageBody>
{
    public ContactMessageValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).Matches(@"^0\d{9,10}$").When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
    }
}

public sealed record BusinessRequestBody(
    string Organization,
    string Contact,
    string Phone,
    string? Email,
    string? Items,
    string? Description,
    string? Deadline);

public sealed class BusinessRequestValidator : AbstractValidator<BusinessRequestBody>
{
    public BusinessRequestValidator()
    {
        RuleFor(x => x.Organization).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Contact).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).Matches(@"^0\d{9,10}$");
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Deadline).MaximumLength(100);
    }
}

public sealed record BulkOrderBody(
    string Organization,
    string Contact,
    string Phone,
    string? Email,
    string? Items,
    string? Note);

public sealed class BulkOrderValidator : AbstractValidator<BulkOrderBody>
{
    public BulkOrderValidator()
    {
        RuleFor(x => x.Organization).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Contact).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).Matches(@"^0\d{9,10}$");
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed record SaveOrganizationBody(
    string Organization,
    string? RegistrationNumber,
    string? EconomicCode,
    string? Province,
    string? City,
    string? Address,
    string? Phone,
    string? Email);

public sealed class SaveOrganizationValidator : AbstractValidator<SaveOrganizationBody>
{
    public SaveOrganizationValidator()
    {
        RuleFor(x => x.Organization).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RegistrationNumber).MaximumLength(50);
        RuleFor(x => x.EconomicCode).MaximumLength(50);
        RuleFor(x => x.Address).MaximumLength(1000);
        RuleFor(x => x.Phone).Matches(@"^0\d{9,10}$").When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Email));
    }
}

// --- checkout ---------------------------------------------------------------

public sealed record CouponBody(string Code, IReadOnlyList<OrderLineBody>? Lines);

public sealed class CouponValidator : AbstractValidator<CouponBody>
{
    public CouponValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);

        // The same basket ceilings the order path applies. Without them this
        // endpoint takes an unbounded array and turns it straight into an `IN`
        // clause: the rate limiter counts requests, and one request carrying a
        // million lines is one request.
        RuleFor(x => x.Lines!).Must(lines => lines.Count <= 50).When(x => x.Lines is not null);
        RuleForEach(x => x.Lines!).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty().MaximumLength(64);
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, 20);
        }).When(x => x.Lines is not null);
    }
}

public sealed record OrderLineBody(string ProductId, int Quantity);

/// <summary>
/// <c>POST /orders</c>'s body, exactly as
/// <c>apps/storefront/src/lib/api/cart.ts</c> sends it.
/// </summary>
/// <remarks>
/// No prices and no totals, on purpose — <c>BACKEND.md</c> Phase 4: "the
/// frontend deliberately sends only ids and quantities and expects you to price
/// it. Do not add prices to this contract."
/// </remarks>
public sealed record PlaceOrderBody(
    IReadOnlyList<OrderLineBody> Lines,
    string AddressId,
    string ShippingMethodId,
    string PaymentMethodId,
    string? CouponCode,
    string? Note);

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderBody>
{
    public PlaceOrderValidator()
    {
        // Same ceilings the frontend's own order route applies, so a request
        // that passed there cannot fail here for a different reason.
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= 50);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty().MaximumLength(64);
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, 20);
        });
        RuleFor(x => x.AddressId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ShippingMethodId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PaymentMethodId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CouponCode).MaximumLength(32);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
