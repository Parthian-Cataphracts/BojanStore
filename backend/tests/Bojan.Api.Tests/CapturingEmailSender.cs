using System.Collections.Concurrent;
using Bojan.Application.Auth;

namespace Bojan.Api.Tests;

/// <summary>
/// Keeps the last message sent to each address so a test can read the reset
/// token out of it.
/// </summary>
/// <remarks>
/// The same role <see cref="CapturingSmsSender"/> plays for the code path.
/// Reading the token from the message rather than from the database is
/// deliberate: it is the only copy the customer ever gets, so a test that takes
/// it from anywhere else would still pass if the mail were never sent.
/// </remarks>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, string> _lastBodyByAddress = new();

    public Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        _lastBodyByAddress[email] = body;
        return Task.CompletedTask;
    }

    /// <summary>The body last sent to this address, or null if nothing was.</summary>
    public string? LastBodyFor(string email) =>
        _lastBodyByAddress.TryGetValue(email, out var body) ? body : null;
}
