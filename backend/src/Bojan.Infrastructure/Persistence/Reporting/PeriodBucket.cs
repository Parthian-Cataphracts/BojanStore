using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Bojan.Infrastructure.Persistence.Reporting;

/// <summary>
/// One row of a grouped time series: a period, and the totals inside it.
/// </summary>
/// <remarks>
/// Keyless — it is the result of a <c>GROUP BY</c>, not a table. Registered on
/// the model so <c>FromSqlRaw</c> has something to materialise into.
/// </remarks>
public sealed class PeriodTotal
{
    /// <summary><c>YYYY-MM-DD</c> or <c>YYYY-MM</c>, depending on the grouping asked for.</summary>
    public string Bucket { get; init; } = string.Empty;

    public long Total { get; init; }

    public int Count { get; init; }

    /// <summary>A second count the series needs — distinct returning customers, for growth.</summary>
    public int SecondaryCount { get; init; }
}

/// <summary>
/// The SQL that turns a timestamp column into a day or a month, per provider.
/// </summary>
/// <remarks>
/// <para>
/// A time series has to group by a date part, and that is the one thing LINQ
/// cannot express portably here: <c>o.PlacedAtUtc.Year</c> translates on
/// PostgreSQL but not through the value converter SQLite needs for ordering
/// (see <see cref="BojanDbContext.ConfigureConventions"/>). Rather than move
/// the bucketing into C# — which <c>BACKEND.md</c> Phase 6 explicitly rules
/// out — the grouping expression is written once per provider and the database
/// still does the work.
/// </para>
/// <para>
/// These fragments are compile-time constants chosen by provider, never
/// anything a caller supplies; the range boundaries around them are ordinary
/// parameters.
/// </para>
/// </remarks>
public static class PeriodBucket
{
    public static bool IsSqlite(this DatabaseFacade database) =>
        database.ProviderName == BojanDbContext.SqliteProviderName;

    /// <summary>The SQL expression that truncates <paramref name="column"/> to a day or a month.</summary>
    public static string Expression(DatabaseFacade database, string column, bool monthly)
    {
        if (database.IsSqlite())
        {
            // The column is EF's DateTimeOffsetToStringConverter format, which
            // starts "yyyy-MM-dd" — so the truncation is a prefix.
            return $"substr(\"{column}\", 1, {(monthly ? 7 : 10)})";
        }

        return $"to_char(\"{column}\" AT TIME ZONE 'UTC', '{(monthly ? "YYYY-MM" : "YYYY-MM-DD")}')";
    }

    /// <summary>
    /// A range boundary in whatever form the provider's column expects.
    /// </summary>
    /// <remarks>
    /// PostgreSQL takes the <see cref="DateTimeOffset"/> itself. SQLite's column
    /// is text in the converter's format, so the parameter has to be that same
    /// text or the comparison would be a string against a timestamp.
    /// </remarks>
    public static object Boundary(DatabaseFacade database, DateTimeOffset value) =>
        database.IsSqlite()
            ? value.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture)
            : value;

    /// <summary>Parses a bucket back into the instant the DTO carries.</summary>
    public static DateTimeOffset Parse(string bucket) =>
        bucket.Length == 7
            ? new DateTimeOffset(
                int.Parse(bucket[..4], CultureInfo.InvariantCulture),
                int.Parse(bucket[5..7], CultureInfo.InvariantCulture),
                1, 0, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(
                DateOnly.ParseExact(bucket, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeOnly.MinValue,
                TimeSpan.Zero);
}
