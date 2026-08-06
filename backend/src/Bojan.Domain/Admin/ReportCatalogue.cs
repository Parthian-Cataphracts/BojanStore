namespace Bojan.Domain.Admin;

/// <summary>
/// The reports the panel can produce, and which roles may see each.
/// </summary>
/// <remarks>
/// <para>
/// The read endpoints are gated one by one — <c>/reports/financial</c> is owner
/// only, the rest are open to any signed-in operator. The export queue was not:
/// it took the report name as a free string, checked nothing but its length,
/// and handed the result back through a download route gated on "any operator"
/// as well. So a support operator could ask for <c>financial</c>, wait for the
/// worker, and download the figures the read endpoint refuses them.
/// </para>
/// <para>
/// The gate lives here rather than in the endpoint because the report name
/// arrives in the request body, which a route-level policy cannot see. Both
/// halves — the queue and the download — consult this, so the export path can
/// no longer be a way around the read path.
/// </para>
/// </remarks>
public static class ReportCatalogue
{
    /// <summary>Every report key the export worker knows how to build.</summary>
    /// <remarks>
    /// An unknown key used to be accepted, queued, and failed asynchronously by
    /// the worker — the operator saw a job appear and then show a raw exception
    /// string. It is refused at the door instead.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, bool> OwnerOnlyByReport =
        new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["sales"] = false,
            ["orders"] = false,
            ["inventory"] = false,
            ["customers"] = false,
            ["campaigns"] = false,
            // Mirrors the owner-only gate on GET /admin/reports/financial.
            ["financial"] = true,
        };

    public static bool IsKnown(string? report) =>
        report is not null && OwnerOnlyByReport.ContainsKey(report);

    /// <summary>
    /// Whether <paramref name="role"/> may export <paramref name="report"/>.
    /// </summary>
    /// <param name="role">
    /// The operator's role in the lowercase spelling the API's claims and the
    /// panel's <c>AdminRole</c> union both use.
    /// </param>
    public static bool CanExport(string? report, string? role)
    {
        if (report is null || !OwnerOnlyByReport.TryGetValue(report, out var ownerOnly))
        {
            return false;
        }

        return !ownerOnly || string.Equals(role, "owner", StringComparison.Ordinal);
    }
}
