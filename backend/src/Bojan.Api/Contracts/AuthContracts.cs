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
public sealed record OtpVerifyResponse(
    string UserId,
    string? FirstName,
    string? LastName,
    bool IsNewUser,
    string Token,
    /// <summary>
    /// The account phone. Present so a password sign-in by *email* still
    /// produces a session that knows the number — the frontend stores it and
    /// had nothing else to fall back on.
    /// </summary>
    string? Phone = null,
    /// <summary>
    /// The account's security stamp, stored in the session cookie and sent back
    /// as <c>X-Customer-Stamp</c> on every authenticated call. It is what lets a
    /// password reset end sessions that were signed before it — see
    /// <c>Customer.SecurityStamp</c>.
    /// </summary>
    string? SecurityStamp = null);

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

public sealed record AdminTwoFactorBody(string Challenge, string Code);

public sealed class AdminTwoFactorValidator : AbstractValidator<AdminTwoFactorBody>
{
    public AdminTwoFactorValidator()
    {
        // A JWT, so bounded generously but bounded: without a ceiling this is a
        // megabyte of string handed to a token parser.
        RuleFor(x => x.Challenge).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Code).Matches(@"^\d{6}$");
    }
}

/// <summary>
/// Exact shape <c>apps/admin/.../admin-auth/login/route.ts</c>'s
/// <c>LoginResponse</c> expects.
/// </summary>
/// <remarks>
/// <c>Token</c> is absent when <c>RequiresTwoFactor</c> is set, and
/// <c>Challenge</c> is present instead — the password step alone never yields
/// a credential that opens the panel.
/// </remarks>
public sealed record AdminLoginResponse(
    string Id,
    string Name,
    string Email,
    string Role,
    bool? RequiresTwoFactor,
    string? Token,
    string? Challenge);

// --- password sign-in (screens 09 and 10) -----------------------------------
//
// Beside the one-time code, not instead of it: SMS to Iranian networks fails
// often enough that a shop with only that door loses sales silently.

public sealed record RegisterBody(string Phone, string Email, string Password);

public sealed class RegisterValidator : AbstractValidator<RegisterBody>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Phone).Matches(@"^09\d{9}$")
            .WithMessage("شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200)
            .WithMessage("ایمیل معتبر وارد کنید.");

        // Length only. Whether the password is strong enough is PasswordPolicy's
        // call, in the domain, so registering and resetting cannot disagree —
        // this bound is here so an oversized body is refused before PBKDF2 ever
        // sees it.
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PasswordFieldLength.Max);
    }
}

/// <summary><c>Identity</c> is a phone or an email; the form does not ask which.</summary>
public sealed record PasswordLoginBody(string Identity, string Password);

public sealed class PasswordLoginValidator : AbstractValidator<PasswordLoginBody>
{
    public PasswordLoginValidator()
    {
        RuleFor(x => x.Identity).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PasswordFieldLength.Max);
    }
}

public sealed record ForgotPasswordBody(string Email);

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordBody>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
    }
}

public sealed record ResetPasswordBody(string Token, string Password);

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordBody>
{
    public ResetPasswordValidator()
    {
        // 64 hex characters, the shape CustomerPasswordService generates.
        RuleFor(x => x.Token).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PasswordFieldLength.Max);
    }
}

/// <summary>Mirrors <c>PasswordPolicy.MaxLength</c> so the two cannot drift.</summary>
internal static class PasswordFieldLength
{
    public const int Max = Bojan.Domain.Identity.PasswordPolicy.MaxLength;
}
