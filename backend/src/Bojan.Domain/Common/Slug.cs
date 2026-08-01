using System.Globalization;
using System.Text;

namespace Bojan.Domain.Common;

/// <summary>
/// Turns a title into a URL-safe slug.
/// </summary>
/// <remarks>
/// Persian titles are the normal case here, and Persian is not transliterated:
/// a slug keeps its own script, because the storefront's URLs are already
/// Persian-facing and a romanised guess would be worse than the original. What
/// is stripped is punctuation, and what is normalised is whitespace and the
/// Arabic characters that have Persian equivalents (ي/ك), so two titles that
/// differ only in keyboard layout cannot produce two different slugs.
/// </remarks>
public static class Slug
{
    private const int MaxLength = 120;

    public static string From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Cannot build a slug from an empty value.", nameof(value));
        }

        var normalised = value
            .Replace('ي', 'ی')  // Arabic yeh -> Persian yeh
            .Replace('ك', 'ک')  // Arabic kaf -> Persian keheh
            .Replace('‌', ' ')       // zero-width non-joiner -> space
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

        var builder = new StringBuilder(normalised.Length);
        var lastWasSeparator = false;

        foreach (var character in normalised)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category is UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter
                or UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
                lastWasSeparator = false;
                continue;
            }

            if (!lastWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }

        // Every character was punctuation — a title of "!!!" has no slug, and
        // returning "" would silently collide with the next one.
        return slug.Length == 0
            ? throw new ArgumentException($"'{value}' contains no characters a slug can be made from.", nameof(value))
            : slug;
    }

    /// <summary>Appends <c>-2</c>, <c>-3</c>… until <paramref name="isTaken"/> says the slug is free.</summary>
    public static string Unique(string desired, Func<string, bool> isTaken)
    {
        if (!isTaken(desired))
        {
            return desired;
        }

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var candidate = $"{desired}-{suffix}";
            if (!isTaken(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Could not find a free slug based on '{desired}'.");
    }
}
