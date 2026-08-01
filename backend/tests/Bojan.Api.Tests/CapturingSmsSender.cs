using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Bojan.Application.Auth;

namespace Bojan.Api.Tests;

/// <summary>
/// Test double for <see cref="ISmsSender"/>.
/// </summary>
/// <remarks>
/// The OTP code is never returned in any HTTP response and the store only
/// ever holds its hash — by design, see <c>OtpChallenge</c>'s remarks — so a
/// test that wants to complete the flow needs the code from somewhere. This
/// replaces the same side effect <see cref="Bojan.Infrastructure.Auth.ConsoleSmsSender"/>
/// performs in production (log instead of send); everything upstream of it —
/// validation, hashing, rate limiting, the endpoint itself — runs unmodified.
/// </remarks>
public sealed partial class CapturingSmsSender : ISmsSender
{
    private readonly ConcurrentDictionary<string, string> _lastCodeByPhone = new();

    public Task SendAsync(string phone, string message, CancellationToken cancellationToken)
    {
        var match = CodePattern().Match(message);
        if (match.Success)
        {
            _lastCodeByPhone[phone] = match.Value;
        }

        return Task.CompletedTask;
    }

    public string LastCodeFor(string phone) =>
        _lastCodeByPhone.TryGetValue(phone, out var code)
            ? code
            : throw new InvalidOperationException($"No OTP was sent to {phone}.");

    [GeneratedRegex(@"\d{5}$")]
    private static partial Regex CodePattern();
}
