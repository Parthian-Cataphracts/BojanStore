using System.IO.Compression;
using System.Security;
using System.Text;

namespace Bojan.Infrastructure.Jobs;

/// <summary>
/// Turns a report's rows into a real Excel workbook.
/// </summary>
/// <remarks>
/// <para>
/// The panel offered Excel, CSV and PDF and defaulted to Excel; the worker
/// built CSV and threw on the other two. So the ordinary use of the export
/// screen produced a failed job. The refusal is now honest at the queue, and
/// this closes the gap for the format most people actually wanted — a
/// spreadsheet, which is what a report is.
/// </para>
/// <para>
/// Written by hand rather than by taking a dependency. An .xlsx is a zip of
/// XML parts and the subset needed for one sheet of flat rows is small: the
/// package relationships, a workbook, one worksheet, and a style for the header
/// and the dates. <c>System.IO.Compression</c> is already here — the backup
/// worker builds its archive with it — so this adds no supply chain, no
/// licence question and nothing to keep up to date.
/// </para>
/// <para>
/// Values are written as inline strings except for numbers and dates. Inline
/// strings avoid the shared-strings table entirely, which is the part of the
/// format that carries most of its complexity and all of its indexing bugs, and
/// costs nothing at report sizes. Everything is UTF-8, so Persian text needs no
/// special handling of the kind the CSV writer's byte-order mark exists for.
/// </para>
/// </remarks>
public static class XlsxWriter
{
    /// <summary>
    /// The same neutralisation the CSV writer applies, for the same reason.
    /// </summary>
    /// <remarks>
    /// A report carries text customers typed into anonymous forms, and a cell
    /// beginning <c>=</c> is evaluated when the sheet is opened. It matters more
    /// here than in CSV, not less: this file *is* a spreadsheet, so there is no
    /// step where somebody might have opened it in something else.
    /// </remarks>
    private static string Neutralise(string text) =>
        text.Length > 0 && Array.IndexOf(FormulaLeaders, text[0]) >= 0 ? $"'{text}" : text;

    private static readonly char[] FormulaLeaders = ['=', '+', '-', '@', '\t', '\r'];

    public static byte[] Write<T>(IReadOnlyList<T> rows)
    {
        var properties = typeof(T).GetProperties();

        var sheet = new StringBuilder();
        sheet.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sheet.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");

        // Columns wide enough to read. Excel's default is eight characters,
        // which truncates every Persian header this ships with.
        sheet.Append("<cols>");
        for (var index = 0; index < properties.Length; index++)
        {
            sheet.Append($"""<col min="{index + 1}" max="{index + 1}" width="24" customWidth="1"/>""");
        }

        sheet.Append("</cols><sheetData>");

        sheet.Append("""<row r="1">""");
        for (var index = 0; index < properties.Length; index++)
        {
            sheet.Append(InlineString(Reference(index, 1), properties[index].Name, styleIndex: 1));
        }

        sheet.Append("</row>");

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var number = rowIndex + 2;
            sheet.Append($"""<row r="{number}">""");

            for (var column = 0; column < properties.Length; column++)
            {
                sheet.Append(Cell(Reference(column, number), properties[column].GetValue(rows[rowIndex])));
            }

            sheet.Append("</row>");
        }

        sheet.Append("</sheetData></worksheet>");

        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "[Content_Types].xml", ContentTypes);
            Add(archive, "_rels/.rels", PackageRelationships);
            Add(archive, "xl/workbook.xml", Workbook);
            Add(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships);
            Add(archive, "xl/styles.xml", Styles);
            Add(archive, "xl/worksheets/sheet1.xml", sheet.ToString());
        }

        return buffer.ToArray();
    }

    private static void Add(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>The A1-style reference for a zero-based column and a one-based row.</summary>
    private static string Reference(int column, int row)
    {
        var name = string.Empty;

        for (var remaining = column; ; remaining = remaining / 26 - 1)
        {
            name = (char)('A' + remaining % 26) + name;
            if (remaining < 26) break;
        }

        return $"{name}{row}";
    }

    private static string Cell(string reference, object? value) => value switch
    {
        null => $"""<c r="{reference}"/>""",
        // Dates as text in the shop's own format rather than as an Excel
        // serial: a serial needs the reader's locale to render it, and this
        // sheet is read by people whose spreadsheet is set to a Persian
        // calendar and by people whose is not.
        DateTimeOffset at => InlineString(reference, at.ToString("yyyy-MM-dd HH:mm"), styleIndex: 0),
        DateTime at => InlineString(reference, at.ToString("yyyy-MM-dd HH:mm"), styleIndex: 0),
        bool flag => InlineString(reference, flag ? "بله" : "خیر", styleIndex: 0),
        // Numbers stay numbers, which is the entire reason somebody asked for
        // a spreadsheet instead of the CSV.
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
            $"""<c r="{reference}"><v>{Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)}</v></c>""",
        _ => InlineString(reference, value.ToString() ?? string.Empty, styleIndex: 0),
    };

    private static string InlineString(string reference, string text, int styleIndex) =>
        $"""<c r="{reference}" s="{styleIndex}" t="inlineStr"><is><t xml:space="preserve">{SecurityElement.Escape(Neutralise(text))}</t></is></c>""";

    private const string ContentTypes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;

    private const string PackageRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    /// <summary>
    /// One sheet, right to left — the reports are Persian and a workbook that
    /// opens left to right puts the first column where the eye finishes.
    /// </summary>
    private const string Workbook = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets><sheet name="Report" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;

    private const string WorkbookRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    /// <summary>Two cell formats: ordinary, and the bold one the header row uses.</summary>
    private const string Styles = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
          <fills count="1"><fill><patternFill patternType="none"/></fill></fills>
          <borders count="1"><border/></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="2">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
          </cellXfs>
        </styleSheet>
        """;
}
