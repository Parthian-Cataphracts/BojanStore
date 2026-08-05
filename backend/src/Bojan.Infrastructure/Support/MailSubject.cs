using System.Text.RegularExpressions;

namespace Bojan.Infrastructure.Support;

/// <summary>
/// Reduces a mail subject to the topic it is about.
/// </summary>
/// <remarks>
/// This is half of what makes a thread a thread. Two messages belong to the
/// same conversation when they share an outside party and this — so getting it
/// wrong either scatters one exchange across several rows or piles unrelated
/// topics from the same customer into one.
/// </remarks>
public static partial class MailSubject
{
    /// <summary>
    /// Strips reply and forward prefixes, so "Re: Fwd: سفارش" is "سفارش".
    /// </summary>
    /// <remarks>
    /// Any run of them rather than one, because a message that has been round
    /// twice carries two — and mail clients do not agree on whether to add
    /// another. The list covers the English and Persian prefixes a customer's
    /// client is likely to prepend.
    /// </remarks>
    public static string Normalize(string? subject)
    {
        var text = (subject ?? string.Empty).Trim();

        while (true)
        {
            var match = ReplyPrefix().Match(text);
            if (!match.Success)
            {
                break;
            }

            text = text[match.Length..].Trim();
        }

        return text;
    }

    [GeneratedRegex(@"^(re|fwd|fw|aw|پاسخ|ارجاع)\s*:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ReplyPrefix();
}
