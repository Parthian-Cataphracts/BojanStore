using System.Text;

namespace Bojan.Domain.Common;

/// <summary>
/// Persian text, reduced to the form two people typing the same word agree on.
/// </summary>
/// <remarks>
/// <para>
/// Persian is written several ways for the same word, and none of them is a
/// mistake. «آبرنگ» and «ابرنگ» differ only in whether the writer bothered with
/// the madda; «کیف» is spelled with Persian <c>ک</c> or Arabic <c>ك</c>
/// depending on the keyboard, and the two are different characters that look
/// identical; <c>ی</c> and <c>ي</c> likewise. A compound is written with a
/// half-space, a full space, or nothing at all — «نیم‌فاصله», «نیم فاصله»,
/// «نیمفاصله». A shopper who types any of these is looking for the same
/// product, and a search that answers «چیزی پیدا نشد» because of a diacritic is
/// telling them the shop does not stock it.
/// </para>
/// <para>
/// So neither side of a comparison is compared as typed. Both are folded to one
/// form first: letters that are variants of each other become one letter, marks
/// that only affect pronunciation are dropped, digits become Latin whichever
/// alphabet they arrived in, and every kind of space — including the half-space
/// — is removed so that a compound matches however it was joined.
/// </para>
/// <para>
/// The same fold exists in SQL as <c>bojan_fold</c>, because the column has to
/// be folded too and doing that in memory would mean reading the whole table.
/// The two must agree exactly or a search silently stops matching, which is why
/// <c>PersianFoldTests</c> runs this method and that function over the same
/// inputs and compares them rather than trusting the pair to stay in step.
/// </para>
/// </remarks>
public static class PersianText
{
    /// <summary>
    /// Characters that become another character, paired with what they become.
    /// </summary>
    /// <remarks>
    /// Order matters only in that this array and <see cref="Replacements"/> are
    /// read together; the SQL function passes the same two strings to
    /// <c>translate</c>, which is what keeps the two implementations honest.
    /// </remarks>
    private const string Mapped =
        "آأإٱ" + // every alef that carries a mark
        "يىئ" + // Arabic yeh, alef maksura, yeh with hamza
        "ك" + //   Arabic kaf
        "ةۀ" + //  teh marbuta, heh with yeh above
        "ؤ" + //   waw with hamza
        "۰۱۲۳۴۵۶۷۸۹" + // Persian digits
        "٠١٢٣٤٥٦٧٨٩"; //  Arabic-Indic digits

    private const string Replacements =
        "اااا" +
        "ییی" +
        "ک" +
        "هه" +
        "و" +
        "0123456789" +
        "0123456789";

    /// <summary>
    /// Characters that are dropped rather than replaced — they change how a
    /// word is pronounced or spaced, never which word it is.
    /// </summary>
    private const string Dropped =
        "ًٌٍَُِّْٰ" + // harakat and superscript alef
        "ـ" + // tatweel, the decorative stretch
        "‌‍" + // zero-width non-joiner and joiner
        " \t\r\n"; // ordinary whitespace: a compound is one word however it was typed

    /// <summary>
    /// The comparison form of <paramref name="value"/>.
    /// </summary>
    /// <remarks>
    /// Lowercased as well, so a SKU or a Latin brand name matches whatever case
    /// it was typed in — the fold is what both sides of every search go through,
    /// and it would be odd for it to fix «ك» and not «B».
    /// </remarks>
    public static string Fold(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (Dropped.Contains(character)) continue;

            var index = Mapped.IndexOf(character);
            builder.Append(index >= 0 ? Replacements[index] : char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>The two strings <c>translate()</c> takes, so the SQL side is written from this one.</summary>
    public static (string From, string To) TranslationTable => (Mapped + Dropped, Replacements);
}
