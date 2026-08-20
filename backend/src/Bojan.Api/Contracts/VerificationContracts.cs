using FluentValidation;

namespace Bojan.Api.Contracts;

// Request bodies for the verification endpoints — see
// Bojan.Api.Endpoints.AccountVerificationEndpoints.

public sealed record RequestPhoneChangeBody(string NewPhone);

public sealed class RequestPhoneChangeValidator : AbstractValidator<RequestPhoneChangeBody>
{
    public RequestPhoneChangeValidator()
    {
        RuleFor(x => x.NewPhone).Matches(@"^09\d{9}$")
            .WithMessage("شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.");
    }
}

public sealed record PhoneVerifyConfirmBody(string Code);

public sealed class PhoneVerifyConfirmValidator : AbstractValidator<PhoneVerifyConfirmBody>
{
    public PhoneVerifyConfirmValidator()
    {
        // The generator produces five digits — see RandomOtpCodeGenerator —
        // but this bound is the shape a verification code is allowed to take
        // in general, not a promise about today's length.
        RuleFor(x => x.Code).Matches(@"^\d{4,6}$")
            .WithMessage("کد تایید را کامل وارد کنید.");
    }
}

public sealed record VerificationSettingsBody(bool RequireEmailVerification, bool RequirePhoneVerification);
