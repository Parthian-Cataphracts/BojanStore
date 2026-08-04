using Bojan.Application.Administration;
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

// --- catalogue -------------------------------------------------------------

/// <summary>
/// Shared bounds for the id every write either creates under or edits.
/// </summary>
/// <remarks>
/// A GUID as text is 36 characters; the ids are parsed rather than trusted, so
/// this only keeps an unbounded string from reaching the parser.
/// </remarks>
internal static class IdRules
{
    public const int MaxLength = 64;
}

public sealed class SaveProductValidator : AbstractValidator<SaveProductRequest>
{
    /// <summary>How many images one product may carry — the gallery screen shows far fewer.</summary>
    private const int MaxImages = 24;

    public SaveProductValidator()
    {
        RuleFor(x => x.Id).MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Title).MaximumLength(300);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Sku).MaximumLength(50);
        RuleFor(x => x.Brand).MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(8000);
        RuleFor(x => x.MetaTitle).MaximumLength(300);
        RuleFor(x => x.MetaDescription).MaximumLength(500);

        // Money is a long of Toman and cannot be negative — the value object
        // throws on one, which would be a 500 rather than a field error.
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).When(x => x.Price.HasValue);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.CompareAt).GreaterThanOrEqualTo(0).When(x => x.CompareAt.HasValue);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0).When(x => x.Stock.HasValue);
        RuleFor(x => x.LowStock).InclusiveBetween(0, 100_000).When(x => x.LowStock.HasValue);

        RuleFor(x => x.Images!).Must(images => images.Count <= MaxImages).When(x => x.Images is not null);
        RuleForEach(x => x.Images!).NotEmpty().MaximumLength(1000).When(x => x.Images is not null);
    }
}

public sealed class ProductPricingValidator : AbstractValidator<ProductPricingRequest>
{
    public ProductPricingValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).When(x => x.Price.HasValue);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.CompareAtPrice).GreaterThanOrEqualTo(0).When(x => x.CompareAtPrice.HasValue);
    }
}

public sealed class ProductDiscountValidator : AbstractValidator<ProductDiscountRequest>
{
    public ProductDiscountValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Percent).InclusiveBetween(0, 99).When(x => x.Percent.HasValue);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).When(x => x.Amount.HasValue);
    }
}

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
        RuleFor(x => x.Id).NotEmpty().MaximumLength(IdRules.MaxLength);
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
        RuleFor(x => x.Id).NotEmpty().MaximumLength(IdRules.MaxLength);
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
        RuleFor(x => x.Id).NotEmpty().MaximumLength(IdRules.MaxLength);
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

public sealed class SaveCategoryValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryValidator()
    {
        RuleFor(x => x.Id).MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.ParentId).MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Icon).MaximumLength(100);
        RuleFor(x => x.Status).MaximumLength(20);
        RuleFor(x => x.MetaTitle).MaximumLength(300);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
        RuleFor(x => x.Order).InclusiveBetween(0, 10_000).When(x => x.Order.HasValue);
    }
}

public sealed class SaveBrandValidator : AbstractValidator<SaveBrandRequest>
{
    public SaveBrandValidator()
    {
        RuleFor(x => x.Id).MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Tagline).MaximumLength(300);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Logo).MaximumLength(1000);
        RuleFor(x => x.Status).MaximumLength(20);
        RuleFor(x => x.MetaTitle).MaximumLength(300);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
    }
}

public sealed class SaveCollectionValidator : AbstractValidator<SaveCollectionRequest>
{
    public SaveCollectionValidator()
    {
        RuleFor(x => x.Id).MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Title).MaximumLength(300);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Summary).MaximumLength(1000);
        RuleFor(x => x.EditorialNote).MaximumLength(4000);
        RuleFor(x => x.Cover).MaximumLength(1000);
        RuleFor(x => x.Status).MaximumLength(20);
    }
}

public sealed class SaveContentValidator : AbstractValidator<SaveContentRequest>
{
    public SaveContentValidator()
    {
        RuleFor(x => x.Id).MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Title).MaximumLength(300);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Kind).MaximumLength(20);
        RuleFor(x => x.Excerpt).MaximumLength(1000);
        RuleFor(x => x.Body).MaximumLength(32000);
        RuleFor(x => x.Cover).MaximumLength(1000);
        RuleFor(x => x.Status).MaximumLength(20);
    }
}

public sealed class SaveCampaignValidator : AbstractValidator<SaveCampaignRequest>
{
    public SaveCampaignValidator()
    {
        RuleFor(x => x.Id).MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Title).MaximumLength(300);
        RuleFor(x => x.Kind).MaximumLength(20);
        RuleFor(x => x.Status).MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}

public sealed class SaveCouponValidator : AbstractValidator<SaveCouponRequest>
{
    public SaveCouponValidator()
    {
        RuleFor(x => x.Id).MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Code).MaximumLength(50);
        // 100% off is a free order, which is what a fixed amount is for.
        RuleFor(x => x.Percent).InclusiveBetween(0, 99).When(x => x.Percent.HasValue);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).When(x => x.Amount.HasValue);
        RuleFor(x => x.MinimumSpend).GreaterThanOrEqualTo(0).When(x => x.MinimumSpend.HasValue);
        RuleFor(x => x.Status).MaximumLength(20);
    }
}

// --- operations ------------------------------------------------------------

public sealed class StockMovementValidator : AbstractValidator<StockMovementRequest>
{
    public StockMovementValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(20);
        // The ceiling is a sanity bound, not a business rule: a stocktake that
        // sets ten million units is a typo, and Adjust would apply it verbatim.
        RuleFor(x => x.Quantity).InclusiveBetween(0, 1_000_000);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}

public sealed class OrderStatusValidator : AbstractValidator<OrderStatusRequest>
{
    public OrderStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.TrackingCode).MaximumLength(100);
    }
}

public sealed class BusinessRequestUpdateValidator : AbstractValidator<BusinessRequestUpdate>
{
    public BusinessRequestUpdateValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Status).MaximumLength(20);
        RuleFor(x => x.AssigneeId).MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Note).MaximumLength(4000);
    }
}

public sealed class SupportReplyValidator : AbstractValidator<SupportReplyRequest>
{
    public SupportReplyValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty().MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(8000);
    }
}

public sealed class CannedReplyValidator : AbstractValidator<CannedReplyRequest>
{
    public CannedReplyValidator()
    {
        RuleFor(x => x.Id).MaximumLength(IdRules.MaxLength);
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Body).MaximumLength(8000);
    }
}

public sealed class ReportExportValidator : AbstractValidator<ReportExportRequest>
{
    public ReportExportValidator()
    {
        RuleFor(x => x.Report).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Format).MaximumLength(10);
    }
}
