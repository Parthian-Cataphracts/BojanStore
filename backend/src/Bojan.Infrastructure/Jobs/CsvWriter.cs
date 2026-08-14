using System.Text;

namespace Bojan.Infrastructure.Jobs;

/// <summary>
/// Turns a report's rows into the CSV an operator downloads.
/// </summary>
/// <remarks>
/// <para>
/// One column per public property, by reflection — six bespoke writers for six
/// flat DTOs would be six places to keep in sync with <c>AdminContracts.cs</c>
/// for no benefit over reading the shape directly off the type.
/// </para>
/// <para>
/// Its own type rather than private helpers on <see cref="ReportExportWorker"/>
/// so that <see cref="Field"/> — which is the whole of this file's security
/// value — can be asserted directly. The worker polls on a timer and writes
/// through a storage port; reaching the escaping rules through that was the
/// reason they had no test.
/// </para>
/// </remarks>
public static class CsvWriter
{
    /// <summary>
    /// Characters a spreadsheet reads as the start of a formula rather than as
    /// text.
    /// </summary>
    /// <remarks>
    /// Reports carry text customers typed — an organisation name from the B2B
    /// form, a support subject — and both of those endpoints are anonymous.
    /// This file exists to be opened in Excel, which is what makes that text
    /// executable: a cell beginning with any of these is evaluated on open, so
    /// <c>=WEBSERVICE(...)</c> in a form field becomes a request from the
    /// operator's machine, and <c>=cmd|...</c> becomes worse. Quoting alone
    /// does not stop it — the formula is still the cell's content once the
    /// quotes are parsed away.
    /// </remarks>
    private static readonly char[] FormulaLeaders = ['=', '+', '-', '@', '\t', '\r'];

    public static byte[] Write<T>(IReadOnlyList<T> rows)
    {
        var properties = typeof(T).GetProperties();
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", properties.Select(p => Field(Header(p)))));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", properties.Select(p => Field(p.GetValue(row)))));
        }

        // UTF-8 BOM so Excel opens Persian text as UTF-8 instead of guessing
        // the system codepage and mangling it.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }


    /// <summary>
    /// The heading a column is written under: its <c>[ReportColumn]</c> when it
    /// has one, and its property name when it does not.
    /// </summary>
    /// <remarks>
    /// The summary reports predate the attribute and still export their own
    /// property names, which is why the fallback stays rather than every DTO
    /// being annotated at once.
    /// </remarks>
    internal static string Header(System.Reflection.PropertyInfo property) =>
        property.GetCustomAttributes(typeof(Bojan.Application.Contracts.ReportColumnAttribute), false)
            .OfType<Bojan.Application.Contracts.ReportColumnAttribute>()
            .FirstOrDefault()?.Header
        ?? property.Name;

    /// <summary>One value as a CSV field: neutralised if it looks like a formula, then quoted if it needs to be.</summary>
    public static string Field(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTimeOffset dt => dt.ToString("yyyy-MM-dd HH:mm"),
            _ => value.ToString() ?? string.Empty,
        };

        // An apostrophe, which every spreadsheet reads as "the rest of this
        // cell is text". Applied before quoting so it lands inside the quoted
        // value rather than beside it.
        if (text.Length > 0 && Array.IndexOf(FormulaLeaders, text[0]) >= 0)
        {
            text = $"'{text}";
        }

        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
