using FluentValidation;

namespace Bojan.Api.Contracts;

// Request/response shapes for the three auth endpoints. Every field name and
// nullability here is load-bearing — see BACKEND.md section 1.3 and the two
// frontend routes these mirror.

public sealed record OtpRequestBody(string Phone);

public sealed class OtpRequestValidator : AbstractValidator<OtpRequestBody>
{
    public OtpRequestValidator()
    {
        // Matches apps/storefront/src/components/auth/LoginForm.tsx's own check —
        // an 11-digit Iranian mobile number starting with 09.
        RuleFor(x => x.Phone).Matches(@"^09\d{9}$")
            .WithMessage("شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.");
    }
}

public sealed record OtpVerifyBody(string Phone, string Code);

public sealed class OtpVerifyValidator : AbstractValidator<OtpVerifyBody>
{
    public OtpVerifyValidator()
    {
        RuleFor(x => x.Phone).Matches(@"^09\d{9}$")
            .WithMessage("شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.");
        RuleFor(x => x.Code).Matches(@"^\d{5}$")
            .WithMessage("کد تایید ۵ رقمی را کامل وارد کنید.");
    }
}

/// <summary>Exact shape <c>apps/storefront/.../otp/verify/route.ts</c> reads from the upstream call.</summary>
public sealed record OtpVerifyResponse(string UserId, string? FirstName, string? LastName, bool IsNewUser, string Token);

public sealed record AdminLoginBody(string Identity, string Password);

public sealed class AdminLoginValidator : AbstractValidator<AdminLoginBody>
{
    public AdminLoginValidator()
    {
        RuleFor(x => x.Identity).NotEmpty();
        // Same bounds the frontend already enforces before it ever reaches here.
        RuleFor(x => x.Password).MinimumLength(8).MaximumLength(200);
    }
}

/// <summary>Exact shape <c>apps/admin/.../admin-auth/login/route.ts</c>'s <c>LoginResponse</c> expects.</summary>
public sealed record AdminLoginResponse(string Id, string Name, string Email, string Role, bool? RequiresTwoFactor, string Token);
