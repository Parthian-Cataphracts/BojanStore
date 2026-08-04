namespace Bojan.Application.Common;

/// <summary>
/// Normalises Persian and Arabic-Indic numerals back to ASCII.
/// </summary>
/// <remarks>
/// <para>
/// The backend counterpart of <c>toLatinDigits</c> in <c>@bojan/ui</c>. The
/// frontend normalises what it validates before posting it, which is why
/// nothing here needed this until now — but the panel's invoice search is a
/// free-text box whose contents are matched against a column of ASCII digits,
/// and an operator with a Persian keyboard types <c>۱۲۳</c>. Normalising in
/// the browser instead would put the rule in the one place a second client
/// would not inherit it from.
/// </para>
/// <para>
/// Both digit sets are mapped: Persian (U+06F0–U+06F9) is what an Iranian
/// keyboard layout produces, Arabic-Indic (U+0660–U+0669) is what text pasted
/// from an Arabic-locale source carries, and they are different code points
/// that render almost identically.
/// </para>
/// </remarks>
public static class PersianDigits
{
    /// <summary>
    /// Everything in <paramref name="value"/> that is a digit in any of the
    /// three sets, as ASCII, with everything else dropped.
    /// </summary>
    /// <remarks>
    /// Dropping rather than preserving the non-digits is what lets an operator
    /// paste an invoice number with the spaces or dashes they read it with and
    /// still match the column.
    /// </remarks>
    public static string ToLatin(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var digits = new char[value.Length];
        var count = 0;

        foreach (var character in value)
        {
            var mapped = character switch
            {
                >= '0' and <= '9' => character,
                >= '۰' and <= '۹' => (char)('0' + (character - '۰')),
                >= '٠' and <= '٩' => (char)('0' + (character - '٠')),
                _ => '\0',
            };

            if (mapped != '\0')
            {
                digits[count++] = mapped;
            }
        }

        return new string(digits, 0, count);
    }
}
