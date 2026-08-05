using System.Globalization;

namespace Bojan.Application.Common;

/// <summary>
/// Formats numbers and dates the way the shop shows them.
/// </summary>
/// <remarks>
/// <para>
/// The backend has not needed this before: every figure a customer sees is
/// rendered by <c>@bojan/ui</c> in the browser, and the API sends raw numbers.
/// An email is the exception — it is composed here and read in a mail client
/// that will never run the shop's formatters, so the text has to arrive already
/// in the shop's own voice.
/// </para>
/// <para>
/// Deliberately the same rules as <c>format.ts</c>: Persian digits with an
/// ASCII thousands separator, which is <em>not</em> what
/// <c>fa-IR</c> produces — it groups with U+066C. So the grouping is done in an
/// invariant culture and the digits transliterated afterwards, exactly as the
/// frontend does it.
/// </para>
/// </remarks>
public static class PersianFormat
{
    private static readonly char[] PersianDigitChars = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];

    private static readonly string[] MonthNames =
    [
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند",
    ];

    private static readonly PersianCalendar Calendar = new();

    /// <summary>Every ASCII digit in the text as its Persian counterpart.</summary>
    public static string Digits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return string.Create(value.Length, value, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                var character = source[index];
                span[index] = character is >= '0' and <= '9'
                    ? PersianDigitChars[character - '0']
                    : character;
            }
        });
    }

    /// <summary>A count — Persian digits, no grouping.</summary>
    public static string Number(int value) => Digits(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>An amount in Toman, grouped and in Persian digits, without the unit.</summary>
    public static string Amount(long value) => Digits(value.ToString("#,##0", CultureInfo.InvariantCulture));

    /// <summary>An amount with the unit — what a customer reads as a price.</summary>
    public static string Money(long value) => $"{Amount(value)} تومان";

    /// <summary>
    /// A Jalali date: <c>۱۵ مرداد ۱۴۰۵</c>.
    /// </summary>
    /// <remarks>
    /// Converted in Tehran's own offset rather than UTC. An order placed at
    /// half past two in the morning Tehran time is stored as the previous
    /// evening in UTC, and telling the customer it was placed a day earlier
    /// than their receipt says is the kind of small wrongness nobody reports
    /// and everybody notices.
    /// </remarks>
    public static string Date(DateTimeOffset value)
    {
        var local = ToTehran(value);
        return $"{Digits(Calendar.GetDayOfMonth(local).ToString(CultureInfo.InvariantCulture))} "
            + $"{MonthNames[Calendar.GetMonth(local) - 1]} "
            + $"{Digits(Calendar.GetYear(local).ToString(CultureInfo.InvariantCulture))}";
    }

    /// <summary>The same, with the time — for a security notice, where the hour matters.</summary>
    public static string DateTime(DateTimeOffset value)
    {
        var local = ToTehran(value);
        return $"{Date(value)}، ساعت {Digits(local.ToString("HH:mm", CultureInfo.InvariantCulture))}";
    }

    /// <summary>
    /// Tehran's wall clock for an instant.
    /// </summary>
    /// <remarks>
    /// Looked up by id, with both the IANA and the Windows spelling tried,
    /// because the two platforms this runs on do not agree on the name. Falling
    /// back to a fixed +03:30 rather than throwing: Iran has not observed
    /// daylight saving since 2022, so the offset is correct, and an email is
    /// not worth failing over a missing time-zone database.
    /// </remarks>
    private static DateTime ToTehran(DateTimeOffset value)
    {
        foreach (var id in new[] { "Asia/Tehran", "Iran Standard Time" })
        {
            try
            {
                return TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById(id)).DateTime;
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the other spelling.
            }
            catch (InvalidTimeZoneException)
            {
                // A corrupt entry in the database — same treatment.
            }
        }

        return value.ToOffset(TimeSpan.FromMinutes(210)).DateTime;
    }
}
