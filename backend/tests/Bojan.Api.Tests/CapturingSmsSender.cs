using System.Collections.Concurrent;
using Bojan.Application.Auth;

namespace Bojan.Api.Tests;

/// <summary>
/// Test double for <see cref="ISmsSender"/>.
/// </summary>
/// <remarks>
/// <para>
/// The OTP code is never returned in any HTTP response and the store only
/// ever holds its hash — by design, see <c>OtpChallenge</c>'s remarks — so a
/// test that wants to complete the flow needs the code from somewhere. This
/// replaces the same side effect <see cref="Bojan.Infrastructure.Auth.ConsoleSmsSender"/>
/// performs when no provider is configured (log instead of send); everything
/// upstream of it — validation, hashing, rate limiting, the endpoint itself —
/// runs unmodified.
/// </para>
/// <para>
/// The code arrives on its own now rather than inside a message to be pattern
/// matched, because the real providers take it that way: the wording of a
/// verification SMS lives in a template registered with the provider, not in
/// this codebase. Free-text sends are recorded separately, and a test that
/// asserts on a campaign body reads those.
/// </para>
/// </remarks>
public sealed class CapturingSmsSender : ISmsSender
{
    private readonly ConcurrentDictionary<string, string> _lastCodeByPhone = new();

    private readonly ConcurrentQueue<(string Phone, string Message)> _messages = new();

    /// <summary>Every free-text message sent, in order — campaigns and notices.</summary>
    public IReadOnlyCollection<(string Phone, string Message)> Messages => [.. _messages];

    public Task SendAsync(string phone, string message, CancellationToken cancellationToken)
    {
        _messages.Enqueue((phone, message));
        return Task.CompletedTask;
    }

    public Task SendVerificationAsync(string phone, string code, CancellationToken cancellationToken)
    {
        _lastCodeByPhone[phone] = code;
        return Task.CompletedTask;
    }

    public string LastCodeFor(string phone) =>
        _lastCodeByPhone.TryGetValue(phone, out var code)
            ? code
            : throw new InvalidOperationException($"No OTP was sent to {phone}.");
}
