using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Bojan.Infrastructure.Jobs;

/// <summary>
/// Turns a report's rows into a printable PDF.
/// </summary>
/// <remarks>
/// <para>
/// The format the queue used to refuse. The reason it refused was real — a PDF
/// carries no fonts of its own, and a writer that neither embeds one nor shapes
/// Persian produces a page of disconnected, reversed glyphs, which is worse
/// than no file. So both of those are done here rather than hoped for:
/// Vazirmatn is embedded from <c>Assets/</c>, and the whole document is laid
/// out right to left.
/// </para>
/// <para>
/// The font ships in the repository instead of being taken from the host. A
/// container image has no Persian font installed, and one that resolved to
/// whatever the machine happened to have would produce a different file on
/// every host — including a blank one.
/// </para>
/// <para>
/// Landscape, because these tables are wide: thirteen columns of an itemised
/// sales report do not fit across a portrait page without shrinking the text to
/// something nobody can read.
/// </para>
/// </remarks>
public static class PdfWriter
{
    private static readonly object Gate = new();
    private static bool _ready;

    /// <summary>
    /// Registers the licence and the embedded font, once per process.
    /// </summary>
    /// <remarks>
    /// QuestPDF refuses to render until told which licence it is used under, and
    /// registering a font twice throws. The worker builds reports on a timer and
    /// may well build two at once, so this is guarded rather than left to
    /// whichever export happens to be first.
    /// </remarks>
    private static void EnsureReady()
    {
        if (_ready) return;

        lock (Gate)
        {
            if (_ready) return;

            QuestPDF.Settings.License = LicenseType.Community;

            var assembly = typeof(PdfWriter).Assembly;
            foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.Ordinal)))
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                QuestPDF.Drawing.FontManager.RegisterFont(stream);
            }

            _ready = true;
        }
    }

    private const string Font = "Vazirmatn";

    public static byte[] Write<T>(
        IReadOnlyList<T> rows,
        string title,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        EnsureReady();

        var properties = typeof(T).GetProperties();
        var headers = properties.Select(CsvWriter.Header).ToArray();

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(style => style.FontFamily(Font).FontSize(8));
                // The whole page, not only the strings in it: the column order
                // has to run right to left as well, or a Persian table reads
                // backwards however well each cell is shaped.
                page.ContentFromRightToLeft();

                page.Header().Column(column =>
                {
                    column.Item().Text(title).FontFamily(Font).FontSize(15).Bold();
                    column.Item().PaddingTop(2).Text(
                        $"از {JalaliDate(fromUtc)} تا {JalaliDate(toUtc)} — {ToPersianDigits(rows.Count)} ردیف")
                        .FontFamily(Font).FontSize(8).FontColor(Colors.Grey.Darken1);
                    column.Item().PaddingTop(6).LineHorizontal(0.6f).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in headers) columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var text in headers)
                        {
                            header.Cell()
                                .Background(Colors.Grey.Lighten3)
                                .Padding(3)
                                .Text(text).FontFamily(Font).FontSize(8).Bold();
                        }
                    });

                    var striped = false;
                    foreach (var row in rows)
                    {
                        striped = !striped;

                        foreach (var property in properties)
                        {
                            table.Cell()
                                // Banded rather than fully ruled: a thirteen
                                // column table with a border on every cell is
                                // harder to read across, not easier.
                                .Background(striped ? Colors.White : Colors.Grey.Lighten5)
                                .Padding(3)
                                .Text(Cell(property.GetValue(row))).FontFamily(Font).FontSize(8);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontFamily(Font).FontSize(7).FontColor(Colors.Grey.Darken1));
                    text.Span("صفحه ");
                    text.CurrentPageNumber();
                    text.Span(" از ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// One value as printed text.
    /// </summary>
    /// <remarks>
    /// Numbers are grouped and given Persian digits, because this file is read
    /// rather than parsed — the CSV and the spreadsheet are what a machine
    /// opens, and both keep their values raw. Nothing here needs the formula
    /// neutralisation the CSV does: a PDF cell is not evaluated by anything.
    /// </remarks>
    private static string Cell(object? value) => value switch
    {
        null => "",
        long number => ToPersianDigits(number.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)),
        int number => ToPersianDigits(number.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)),
        decimal number => ToPersianDigits(number.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)),
        bool flag => flag ? "بله" : "خیر",
        DateTimeOffset instant => JalaliDate(instant),
        _ => ToPersianDigits(value.ToString() ?? ""),
    };

    private static readonly System.Globalization.PersianCalendar Persian = new();

    private static string JalaliDate(DateTimeOffset instant)
    {
        var local = instant.ToOffset(TimeSpan.FromMinutes(210)).DateTime;
        return ToPersianDigits(
            $"{Persian.GetYear(local):0000}/{Persian.GetMonth(local):00}/{Persian.GetDayOfMonth(local):00}");
    }

    /// <summary>Latin digits to Persian ones, leaving every other character alone.</summary>
    private static string ToPersianDigits(string value)
    {
        Span<char> buffer = value.Length <= 256 ? stackalloc char[value.Length] : new char[value.Length];

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            buffer[index] = character is >= '0' and <= '9' ? (char)(character - '0' + '۰') : character;
        }

        return new string(buffer);
    }

    private static string ToPersianDigits(int value) =>
        ToPersianDigits(value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
}
