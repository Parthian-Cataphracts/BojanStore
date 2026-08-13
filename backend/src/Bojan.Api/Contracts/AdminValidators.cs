using Bojan.Application.Administration;
using Bojan.Domain.Admin;
using FluentValidation;

namespace Bojan.Api.Contracts;

/// <summary>
/// Bounds on the panel's writes.
/// </summary>
/// <remarks>
/// <para>
/// The panel checks its own forms and forwards only the fields each resource
/// declares (<c>apps/admin/src/lib/api/resources.ts</c>), but that check runs on
/// a machine a request can skip — the same reason every storefront write is
/// validated here too. Without these, a value longer than its column produces a
/// database error surfacing as a 500 rather than the field error the form can
/// point at, and a collection with no ceiling becomes an unbounded number of
/// upserts in one request.
/// </para>
/// <para>
/// The lengths mirror the EF configurations exactly, so a value that passes
/// here cannot fail at the database for being too long.
/// </para>
/// </remarks>
public sealed class SettingsValidator : AbstractValidator<SettingsRequest>
{
    /// <summary>
    /// How many keys one save may carry.
    /// </summary>
    /// <remarks>
    /// The largest settings screen posts well under a dozen. This is a ceiling
    /// on abuse, not on the forms.
    /// </remarks>
    private const int MaxKeys = 100;

    public SettingsValidator()
    {
        // SettingEntryConfiguration: Section 50, Key 100, Value 8000.
        RuleFor(x => x.Section).NotEmpty().MaximumLength(50);

        RuleFor(x => x.Values)
            .NotNull()
            .Must(values => values.Count <= MaxKeys)
            .WithMessage($"A settings save may carry at most {MaxKeys} keys.");

        RuleForEach(x => x.Values)
            .Must(pair => pair.Key.Length is > 0 and <= 100)
            .WithMessage("A setting key must be between 1 and 100 characters.")
            .Must(pair => (pair.Value?.Length ?? 0) <= 8000)
            .WithMessage("A setting value may be at most 8000 characters.")
            .When(x => x.Values is not null);
    }
}

public sealed class ApiKeyValidator : AbstractValidator<ApiKeyRequest>
{
    public ApiKeyValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Label).MaximumLength(100);
        RuleFor(x => x.Scope).MaximumLength(50);
    }
}

public sealed class BackupValidator : AbstractValidator<BackupRequest>
{
    public BackupValidator()
    {
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(20);
    }
}

/// <summary>
/// The broadcast composer — screen 131.
/// </summary>
/// <remarks>
/// The body is the one field here a caller could make arbitrarily large, and it
/// is stored per recipient.
/// </remarks>
public sealed class BroadcastValidator : AbstractValidator<BroadcastRequest>
{
    public BroadcastValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Channel).MaximumLength(20);
        RuleFor(x => x.Audience).MaximumLength(50);
    }
}

/// <summary>
/// One targeted notification. Bounded to the same lengths as a broadcast, plus
/// a ceiling on the link — the column is 500 and a path longer than this is not
/// a destination anyone typed.
/// </summary>
public sealed class CustomerNotificationValidator : AbstractValidator<CustomerNotificationRequest>
{
    public CustomerNotificationValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Link).MaximumLength(500);
    }
}

public sealed class CustomerBlockValidator : AbstractValidator<CustomerBlockRequest>
{
    public CustomerBlockValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(64);
    }
}

/// <summary>
/// Length only, exactly as the customer's own password endpoints are validated.
/// </summary>
/// <remarks>
/// Whether it is strong enough is <c>PasswordPolicy</c>'s question and is asked
/// in the service, so an operator gets the same sentence about the same rule the
/// customer would have got. The ceiling is here because PBKDF2 runs on whatever
/// arrives, and an unbounded password is a free way to spend the server's CPU.
/// </remarks>
public sealed class CustomerPasswordValidator : AbstractValidator<CustomerPasswordRequest>
{
    public CustomerPasswordValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PasswordFieldLength.Max);
    }
}

// --- the catalogue and content saves ---------------------------------------

/// <summary>
/// Long-form fields — a product description, an article body.
/// </summary>
/// <remarks>
/// These columns are unbounded text rather than varchar, so the database will
/// take whatever arrives. The ceiling is here instead: an article is long, but
/// it is not a megabyte, and nothing downstream is prepared for one.
/// </remarks>
internal static class AdminFieldLengths
{
    public const int Slug = 200;
    public const int Title = 300;
    public const int Name = 200;
    public const int ShortText = 300;
    public const int Url = 1000;
    public const int Description = 8000;
    public const int ArticleBody = 100_000;
}

public sealed class SaveProductValidator : AbstractValidator<SaveProductRequest>
{
    /// <summary>Gallery ceiling. Screen 105 is a grid, not a photo library.</summary>
    private const int MaxImages = 30;

    public SaveProductValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Title).MaximumLength(AdminFieldLengths.Title);
        RuleFor(x => x.Slug).MaximumLength(AdminFieldLengths.Slug);
        RuleFor(x => x.Sku).MaximumLength(50);
        RuleFor(x => x.Brand).MaximumLength(AdminFieldLengths.Slug);
        RuleFor(x => x.Category).MaximumLength(AdminFieldLengths.Slug);
        RuleFor(x => x.Status).MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(AdminFieldLengths.Description);
        RuleFor(x => x.MetaTitle).MaximumLength(AdminFieldLengths.Title);
        RuleFor(x => x.MetaDescription).MaximumLength(500);

        // Money and stock are non-negative wherever they are set; the services
        // check their own invariants, this only rules out the absurd.
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).When(x => x.Price.HasValue);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.CompareAt).GreaterThanOrEqualTo(0).When(x => x.CompareAt.HasValue);
        RuleFor(x => x.Stock).InclusiveBetween(0, 1_000_000).When(x => x.Stock.HasValue);
        RuleFor(x => x.LowStock).InclusiveBetween(0, 100_000).When(x => x.LowStock.HasValue);

        RuleFor(x => x.Images!).Must(images => images.Count <= MaxImages).When(x => x.Images is not null);
        RuleForEach(x => x.Images!).MaximumLength(AdminFieldLengths.Url).When(x => x.Images is not null);
    }
}

public sealed class SaveCategoryValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Title).MaximumLength(AdminFieldLengths.Name);
        RuleFor(x => x.Slug).MaximumLength(AdminFieldLengths.Slug);
        RuleFor(x => x.ParentId).MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(AdminFieldLengths.Description);
        RuleFor(x => x.Icon).MaximumLength(100);
        RuleFor(x => x.Status).MaximumLength(20);
        RuleFor(x => x.MetaTitle).MaximumLength(AdminFieldLengths.Title);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
        RuleFor(x => x.Order).InclusiveBetween(0, 10_000).When(x => x.Order.HasValue);
    }
}

public sealed class SaveBrandValidator : AbstractValidator<SaveBrandRequest>
{
    public SaveBrandValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Title).MaximumLength(AdminFieldLengths.Name);
        RuleFor(x => x.Slug).MaximumLength(AdminFieldLengths.Slug);
        RuleFor(x => x.Tagline).MaximumLength(AdminFieldLengths.ShortText);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(AdminFieldLengths.Description);
        RuleFor(x => x.Logo).MaximumLength(AdminFieldLengths.Url);
        RuleFor(x => x.Status).MaximumLength(20);
        RuleFor(x => x.MetaTitle).MaximumLength(AdminFieldLengths.Title);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
    }
}

public sealed class SaveCollectionValidator : AbstractValidator<SaveCollectionRequest>
{
    public SaveCollectionValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Title).MaximumLength(AdminFieldLengths.Title);
        RuleFor(x => x.Slug).MaximumLength(AdminFieldLengths.Slug);
        RuleFor(x => x.Description).MaximumLength(AdminFieldLengths.Description);
        RuleFor(x => x.Summary).MaximumLength(1000);
        RuleFor(x => x.EditorialNote).MaximumLength(4000);
        RuleFor(x => x.Cover).MaximumLength(AdminFieldLengths.Url);
        RuleFor(x => x.Status).MaximumLength(20);
    }
}

/// <summary>
/// A magazine article. The body is the one genuinely long field — an article,
/// not a caption — and the rest mirror the columns.
/// </summary>
public sealed class SaveArticleValidator : AbstractValidator<SaveArticleRequest>
{
    public SaveArticleValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Title).MaximumLength(AdminFieldLengths.Title);
        RuleFor(x => x.Slug).MaximumLength(AdminFieldLengths.Slug);
        RuleFor(x => x.Excerpt).MaximumLength(AdminFieldLengths.Description);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.Cover).MaximumLength(AdminFieldLengths.Url);
        RuleFor(x => x.Body).MaximumLength(AdminFieldLengths.ArticleBody);
        RuleFor(x => x.Status).MaximumLength(20);
    }
}

public sealed class SaveContentValidator : AbstractValidator<SaveContentRequest>
{
    public SaveContentValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Title).MaximumLength(AdminFieldLengths.Title);
        RuleFor(x => x.Slug).MaximumLength(AdminFieldLengths.Slug);
        RuleFor(x => x.Kind).MaximumLength(20);
        // The one field that is genuinely long — an article, not a caption.
        RuleFor(x => x.Body).MaximumLength(AdminFieldLengths.ArticleBody);
        RuleFor(x => x.Excerpt).MaximumLength(AdminFieldLengths.Description);
        RuleFor(x => x.Cover).MaximumLength(AdminFieldLengths.Url);
        RuleFor(x => x.Status).MaximumLength(20);
    }
}

public sealed class SaveCampaignValidator : AbstractValidator<SaveCampaignRequest>
{
    public SaveCampaignValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Title).MaximumLength(AdminFieldLengths.Title);
        RuleFor(x => x.Kind).MaximumLength(20);
        RuleFor(x => x.Status).MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(AdminFieldLengths.Description);

        // A campaign that ends before it starts never runs, and nothing
        // downstream would explain why.
        RuleFor(x => x.EndsAt)
            .GreaterThanOrEqualTo(x => x.StartsAt!.Value)
            .When(x => x.StartsAt.HasValue && x.EndsAt.HasValue)
            .WithMessage("A campaign cannot end before it starts.");
    }
}

public sealed class SaveCouponValidator : AbstractValidator<SaveCouponRequest>
{
    public SaveCouponValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Code).MaximumLength(32);
        // 1..99, matching SaveCouponAsync exactly: it refuses 100 and above,
        // and a validator that let 100 through would only move that rejection
        // from a field error to a service one.
        RuleFor(x => x.Percent).InclusiveBetween(1, 99).When(x => x.Percent.HasValue);
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount.HasValue);
        RuleFor(x => x.MinimumSpend).GreaterThanOrEqualTo(0).When(x => x.MinimumSpend.HasValue);
        RuleFor(x => x.Status).MaximumLength(20);
    }
}

public sealed class ProductPricingValidator : AbstractValidator<ProductPricingRequest>
{
    public ProductPricingValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).When(x => x.Price.HasValue);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.CompareAtPrice).GreaterThanOrEqualTo(0).When(x => x.CompareAtPrice.HasValue);
    }
}

public sealed class ProductDiscountValidator : AbstractValidator<ProductDiscountRequest>
{
    public ProductDiscountValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
        // Same window ApplyDiscountAsync accepts.
        RuleFor(x => x.Percent).InclusiveBetween(1, 99).When(x => x.Percent.HasValue);
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount.HasValue);

        RuleFor(x => x.EndsAt)
            .GreaterThanOrEqualTo(x => x.StartsAt!.Value)
            .When(x => x.StartsAt.HasValue && x.EndsAt.HasValue)
            .WithMessage("A discount cannot end before it starts.");
    }
}

// --- support, orders and inventory ------------------------------------------

public sealed class SupportReplyValidator : AbstractValidator<SupportReplyRequest>
{
    public SupportReplyValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty().MaximumLength(64);
        // SupportMessageConfiguration: Body 8000.
        RuleFor(x => x.Body).NotEmpty().MaximumLength(8000);
    }
}

public sealed class CannedReplyValidator : AbstractValidator<CannedReplyRequest>
{
    public CannedReplyValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Body).MaximumLength(8000);
    }
}

public sealed class OrderStatusValidator : AbstractValidator<OrderStatusRequest>
{
    public OrderStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.TrackingCode).MaximumLength(100);
    }
}

/// <summary>
/// The operator's decision on a return.
/// </summary>
/// <remarks>
/// The note lands in two columns of different widths —
/// <c>ReturnRequest.ReviewNote</c> and <c>ReturnTimelineEvent.Reason</c>, both
/// 2000 — so the bound is theirs, per this file's rule that a value passing here
/// cannot fail at the database for being too long.
/// </remarks>
public sealed class ReturnDecisionValidator : AbstractValidator<ReturnDecisionRequest>
{
    public ReturnDecisionValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed class BusinessRequestUpdateValidator : AbstractValidator<BusinessRequestUpdate>
{
    public BusinessRequestUpdateValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Status).MaximumLength(20);
        RuleFor(x => x.AssigneeId).MaximumLength(64);
        RuleFor(x => x.Note).MaximumLength(4000);
    }
}

public sealed class StockMovementValidator : AbstractValidator<StockMovementRequest>
{
    public StockMovementValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(20);
        // A movement of nothing is not a movement; the ceiling is a sanity
        // bound, since the service checks stock for an outward one.
        RuleFor(x => x.Quantity).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(200);
        // The floor is the use case's own constant, so the two cannot drift —
        // they used to say twelve and eight, and the panel's form said ten.
        // The ceiling only stops a megabyte reaching the hasher, which is CPU
        // this endpoint would otherwise spend on request.
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(AdminOperationsService.MinimumPasswordLength)
            .MaximumLength(200);
    }
}

public sealed class TwoFactorValidator : AbstractValidator<TwoFactorRequest>
{
    public TwoFactorValidator()
    {
        RuleFor(x => x.Code).Matches(@"^\d{6}$");
        // 32 base32 characters is what Totp.GenerateSecret produces; the
        // ceiling allows for grouping spaces an authenticator app may show.
        RuleFor(x => x.Secret).MaximumLength(64);
    }
}

// --- the operators themselves ------------------------------------------------

/// <summary>
/// Screen 145's form.
/// </summary>
/// <remarks>
/// <para>
/// The lengths mirror <c>AdminUserConfiguration</c> exactly, per this file's
/// rule. The phone pattern is the storefront's, because the panel's sign-in
/// screen tests the identity it is given against that same shape — a number in
/// any other format is one this account can never sign in with.
/// </para>
/// <para>
/// Whether the address is already somebody else's, whether the role exists and
/// whether the password is long enough are all decided in the service: two of
/// them need the database and the third has to hold for
/// <see cref="AdminUserPasswordValidator"/> too. What is here is the shape.
/// </para>
/// </remarks>
public sealed class SaveAdminUserValidator : AbstractValidator<SaveAdminUserRequest>
{
    public SaveAdminUserValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Name).MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(200);
        RuleFor(x => x.Role).MaximumLength(20);
        RuleFor(x => x.Password).MaximumLength(PasswordFieldLength.Max);

        RuleFor(x => x.Phone)
            .Matches(@"^09\d{9}$")
            .When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("شماره موبایل باید با ۰۹ شروع شود و ۱۱ رقم باشد.");
    }
}

/// <summary>
/// Length only, exactly as the customer's password endpoints are validated —
/// how long is long enough is the service's question, so setting a password for
/// somebody gets the same answer as changing your own.
/// </summary>
public sealed class AdminUserPasswordValidator : AbstractValidator<AdminUserPasswordRequest>
{
    public AdminUserPasswordValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PasswordFieldLength.Max);
    }
}

public sealed class AdminUserTwoFactorValidator : AbstractValidator<AdminUserTwoFactorRequest>
{
    public AdminUserTwoFactorValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
    }
}

// --- catalogue -------------------------------------------------------------


/// <summary>
/// Screens 106-108 post whole lists, so the ceilings here are on collection
/// size as much as on field length — the service applies the same numbers, and
/// these turn a refusal into a field error rather than a bare 400.
/// </summary>
public sealed class SaveVariantsValidator : AbstractValidator<SaveVariantsRequest>
{
    private const int MaxAxes = 100;
    private const int MaxOptionsPerAxis = 50;

    public SaveVariantsValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Axes).NotNull().Must(axes => axes.Count <= MaxAxes);

        RuleForEach(x => x.Axes).ChildRules(axis =>
        {
            axis.RuleFor(a => a.Key).NotEmpty().MaximumLength(100);
            axis.RuleFor(a => a.Label).NotEmpty().MaximumLength(100);
            axis.RuleFor(a => a.Kind).MaximumLength(20);
            axis.RuleFor(a => a.Options).NotNull().Must(options => options.Count <= MaxOptionsPerAxis);
            axis.RuleForEach(a => a.Options).ChildRules(option =>
            {
                option.RuleFor(o => o.Key).NotEmpty().MaximumLength(100);
                option.RuleFor(o => o.Label).NotEmpty().MaximumLength(100);
                option.RuleFor(o => o.Hex).MaximumLength(9);
            });
        }).When(x => x.Axes is not null);
    }
}



public sealed class SaveSkusValidator : AbstractValidator<SaveSkusRequest>
{
    private const int MaxSkus = 100;

    public SaveSkusValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Skus).NotNull().Must(skus => skus.Count <= MaxSkus);

        RuleForEach(x => x.Skus).ChildRules(sku =>
        {
            sku.RuleFor(s => s.Code).NotEmpty().MaximumLength(64);
            sku.RuleFor(s => s.Barcode).MaximumLength(32);
            sku.RuleFor(s => s.Combination).MaximumLength(200);
            sku.RuleFor(s => s.Price).GreaterThanOrEqualTo(0).When(s => s.Price.HasValue);
            sku.RuleFor(s => s.Stock).GreaterThanOrEqualTo(0).When(s => s.Stock.HasValue);
        }).When(x => x.Skus is not null);
    }
}



public sealed class SaveAttributesValidator : AbstractValidator<SaveAttributesRequest>
{
    private const int MaxAttributes = 100;
    private const int MaxValuesPerAttribute = 100;

    public SaveAttributesValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Attributes).NotNull().Must(attributes => attributes.Count <= MaxAttributes);

        RuleForEach(x => x.Attributes).ChildRules(attribute =>
        {
            attribute.RuleFor(a => a.Name).NotEmpty().MaximumLength(100);
            attribute.RuleFor(a => a.Kind).MaximumLength(20);
            attribute.RuleFor(a => a.Values!)
                .Must(values => values.Count <= MaxValuesPerAttribute)
                .When(a => a.Values is not null);
            attribute.RuleForEach(a => a.Values!).MaximumLength(500).When(a => a.Values is not null);
        }).When(x => x.Attributes is not null);
    }
}



public sealed class ReportExportValidator : AbstractValidator<ReportExportRequest>
{
    public ReportExportValidator()
    {
        // Against the catalogue, not merely a length. A name the worker cannot
        // build used to be queued happily and then fail asynchronously, so the
        // operator watched a job appear and turn into a raw exception message.
        // Which *roles* may ask for a given report is decided in the service —
        // a validator has no view of the caller.
        RuleFor(x => x.Report)
            .NotEmpty()
            .MaximumLength(50)
            .Must(ReportCatalogue.IsKnown)
            .WithMessage("این گزارش شناخته نشد.");

        RuleFor(x => x.Format).MaximumLength(10);
    }
}
