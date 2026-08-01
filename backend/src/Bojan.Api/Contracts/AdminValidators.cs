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
        RuleFor(x => x.Sku).MaximumLength(50);
        RuleFor(x => x.Brand).MaximumLength(AdminFieldLengths.Slug);
        RuleFor(x => x.Category).MaximumLength(AdminFieldLengths.Slug);
        RuleFor(x => x.Status).MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(AdminFieldLengths.Description);

        // Money and stock are non-negative wherever they are set; the services
        // check their own invariants, this only rules out the absurd.
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).When(x => x.Price.HasValue);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.Stock).InclusiveBetween(0, 1_000_000).When(x => x.Stock.HasValue);

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
    }
}

public sealed class SaveBrandValidator : AbstractValidator<SaveBrandRequest>
{
    public SaveBrandValidator()
    {
        RuleFor(x => x.Id).MaximumLength(64);
        RuleFor(x => x.Title).MaximumLength(AdminFieldLengths.Name);
        RuleFor(x => x.Slug).MaximumLength(AdminFieldLengths.Slug);
        RuleFor(x => x.Description).MaximumLength(AdminFieldLengths.Description);
        RuleFor(x => x.Logo).MaximumLength(AdminFieldLengths.Url);
        RuleFor(x => x.Status).MaximumLength(20);
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
        RuleFor(x => x.Cover).MaximumLength(AdminFieldLengths.Url);
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
        // The floor matches AdminAuthService's own rule; the ceiling only stops
        // a megabyte reaching the hasher, which is CPU this endpoint would spend.
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(200);
    }
}
