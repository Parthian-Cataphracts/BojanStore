using System.Security.Cryptography;
using Bojan.Application.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bojan.Infrastructure.Auth;

/// <summary>
/// A cryptographically random five-digit code — five because that is how many
/// boxes screen 51 draws, and <see cref="RandomNumberGenerator"/> rather than
/// <see cref="Random"/> because a guessable sign-in code is not a code.
/// </summary>
public sealed class RandomOtpCodeGenerator : IOtpCodeGenerator
{
    private const int CodeLength = 5;

    public string GenerateFor(string phone)
    {
        Span<char> digits = stackalloc char[CodeLength];
        for (var index = 0; index < CodeLength; index++)
        {
            digits[index] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(digits);
    }
}

/// <summary>
/// The one number whose sign-in code is fixed while developing.
/// </summary>
/// <remarks>
/// Bound from <c>Auth:DevOtp</c>, which only exists in
/// <c>appsettings.Development.json</c>.
/// </remarks>
public sealed class DevOtpOptions
{
    public const string SectionName = "Auth:DevOtp";

    /// <summary>The number the fixed code applies to. Empty disables the whole thing.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>The code that number always receives. Five digits, like every other code.</summary>
    public string Code { get; set; } = string.Empty;

    public bool IsConfigured =>
        Phone.Length > 0 && Code.Length > 0;
}

/// <summary>
/// Hands one known number a fixed sign-in code, so a developer with no SMS
/// gateway can log in.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This can only ever run in Development.</strong> Two independent
/// things have to be true: <c>Program.cs</c> registers this class at all only
/// when <c>IHostEnvironment.IsDevelopment()</c>, and the options it needs live
/// in <c>appsettings.Development.json</c>, which no other environment loads.
/// Setting <c>Auth__DevOtp__Phone</c> on a production host changes nothing,
/// because nothing there reads it.
/// </para>
/// <para>
/// It is a decorator, not a replacement: every other number still gets a real
/// random code from <see cref="RandomOtpCodeGenerator"/>, and the configured
/// number still gets a real challenge row with a real expiry and a real attempt
/// counter. The only thing that is not random is which five digits the
/// challenge was built from — so what a developer exercises is the actual
/// sign-in flow, not a bypass around it.
/// </para>
/// <para>
/// It also logs a warning on every use. A fixed code is exactly the kind of
/// thing that should be impossible to leave switched on without noticing.
/// </para>
/// </remarks>
public sealed class StaticOtpCodeGenerator(
    RandomOtpCodeGenerator inner,
    IOptions<DevOtpOptions> options,
    ILogger<StaticOtpCodeGenerator> logger) : IOtpCodeGenerator
{
    private readonly DevOtpOptions _options = options.Value;

    public string GenerateFor(string phone)
    {
        if (!_options.IsConfigured || !string.Equals(phone, _options.Phone, StringComparison.Ordinal))
        {
            return inner.GenerateFor(phone);
        }

        logger.LogWarning(
            "Development sign-in: {Phone} was given its fixed code instead of a random one. This is configured by Auth:DevOtp and cannot happen outside Development.",
            phone);

        return _options.Code;
    }
}
