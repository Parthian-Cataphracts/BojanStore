using System.Collections.Concurrent;
using Bojan.Application.Auth;

namespace Bojan.Api.Tests;

/// <summary>One message as it was sent.</summary>
public sealed record CapturedEmail(string Subject, string Body, string? Html);

/// <summary>
/// Keeps what was sent to each address so a test can read it back.
/// </summary>
/// <remarks>
/// The same role <see cref="CapturingSmsSender"/> plays for the code path.
/// Reading the reset token from the message rather than from the database is
/// deliberate: it is the only copy the customer ever gets, so a test that took
/// it from anywhere else would still pass if the mail were never sent.
/// </remarks>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, CapturedEmail> _lastByAddress = new();

    /// <summary>
    /// Everything sent, oldest first.
    /// </summary>
    /// <remarks>
    /// Kept alongside the per-address map because a test that asserts nothing
    /// was sent needs to see the whole set rather than one address.
    /// </remarks>
    public ConcurrentQueue<(string Address, CapturedEmail Email)> All { get; } = new();

    public Task SendAsync(
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken,
        string? html = null)
    {
        var captured = new CapturedEmail(subject, body, html);
        _lastByAddress[email] = captured;
        All.Enqueue((email, captured));
        return Task.CompletedTask;
    }

    /// <summary>The text body last sent to this address, or null if nothing was.</summary>
    public string? LastBodyFor(string email) =>
        _lastByAddress.TryGetValue(email, out var captured) ? captured.Body : null;

    /// <summary>
    /// The reset token out of the link in the last message to this address.
    /// </summary>
    /// <remarks>
    /// Pulled from the link rather than taken as the whole body. It used to be
    /// the whole body — the mail carried the bare token and nothing else, which
    /// a test could read directly and a customer could do nothing with. Reading
    /// it out of the URL asserts the stronger thing: that what arrives is a
    /// link the recipient can actually follow.
    /// </remarks>
    public string? ResetTokenFor(string email)
    {
        var body = LastBodyFor(email);
        if (body is null)
        {
            return null;
        }

        var marker = body.IndexOf("token=", StringComparison.Ordinal);
        if (marker < 0)
        {
            return null;
        }

        var value = body[(marker + "token=".Length)..];

        // The link sits on its own line in the text body, so the token runs
        // to the first thing that could end a URL.
        var end = 0;
        while (end < value.Length
            && !char.IsWhiteSpace(value[end])
            && value[end] is not ('&' or '"'))
        {
            end++;
        }

        return value[..end];
    }

    /// <summary>The whole of the last message to this address.</summary>
    public CapturedEmail? LastFor(string email) =>
        _lastByAddress.TryGetValue(email, out var captured) ? captured : null;

    public void Clear()
    {
        _lastByAddress.Clear();
        All.Clear();
    }
}
