using Bojan.Application.Common;

namespace Bojan.Api.Endpoints;

/// <summary>
/// The storefront's one operating-state question, answered without a
/// credential — see <see cref="IStoreStatusQueries"/>.
/// </summary>
/// <remarks>
/// One indexed row lookup, unauthenticated and rate-limited the same as the
/// rest of this API's public reads — cheap enough that the storefront can
/// call it on every request without a separate cache layer in front of it.
/// </remarks>
public static class StoreStatusEndpoints
{
    public static void MapStoreStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/store/status", GetStoreStatus).AllowAnonymous();

        // The shop's own name, contact details and delivery promises. Cached
        // like the rest of the editorial reads: it changes when an owner edits
        // a settings screen, which is rare, and it is rendered on every page.
        app.MapGet("/store/settings", GetStorefrontSettings).AllowAnonymous().CacheFor(300);
    }

    private static async Task<IResult> GetStoreStatus(
        IStoreStatusQueries queries, CancellationToken cancellationToken)
    {
        var maintenanceMode = await queries.IsMaintenanceModeEnabledAsync(cancellationToken);

        return Results.Ok(new StoreStatusResponse(maintenanceMode));
    }

    private static async Task<IResult> GetStorefrontSettings(
        IStoreStatusQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.GetStorefrontSettingsAsync(cancellationToken));

    private sealed record StoreStatusResponse(bool MaintenanceMode);
}
