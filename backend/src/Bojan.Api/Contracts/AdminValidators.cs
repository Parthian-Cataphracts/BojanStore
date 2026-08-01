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
        // The floor matches AdminAuthService's own rule; the ceiling only stops
        // a megabyte reaching the hasher, which is CPU this endpoint would spend.
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(200);
    }
}
