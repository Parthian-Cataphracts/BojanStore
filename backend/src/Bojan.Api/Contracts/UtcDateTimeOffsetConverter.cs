using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bojan.Api.Contracts;

/// <summary>
/// Brings every instant arriving in a request body to UTC.
/// </summary>
/// <remarks>
/// <para>
/// Npgsql maps <c>DateTimeOffset</c> to <c>timestamp with time zone</c> and
/// refuses to write one whose offset is not zero — it throws rather than
/// converting, on the reasonable grounds that silently discarding an offset is
/// how time zones get lost. That throw surfaces as a
/// <c>DbUpdateException</c>, which this API answers as a conflict, so a caller
/// who sent a perfectly valid RFC 3339 timestamp in their own offset was told
/// the value was already taken.
/// </para>
/// <para>
/// It went unnoticed because everything that posts a date here happens to send
/// <c>Z</c>: the panel's forms all build theirs with
/// <c>new Date(value).toISOString()</c>. The report exporter is the first
/// screen to send Tehran's own offset — a report is asked for in the operator's
/// day, not the server's — and it queued nothing, with the panel reporting a
/// duplicate that did not exist.
/// </para>
/// <para>
/// Converting rather than refusing, because the instant is not ambiguous:
/// <c>2026-08-13T00:00:00+03:30</c> and <c>2026-08-12T20:30:00Z</c> are the
/// same moment, and which of the two a client chose to write is not something
/// the database needs to keep. Formatting for a reader is
/// <c>PersianFormat</c>'s job and it converts to Tehran on the way out.
/// </para>
/// </remarks>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.GetDateTimeOffset().ToUniversalTime();

    /// <summary>
    /// Written the way it always was — the default converter's own format,
    /// which for a value already in UTC ends in <c>Z</c>.
    /// </summary>
    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
