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
    }

    private static async Task<IResult> GetStoreStatus(
        IStoreStatusQueries queries, CancellationToken cancellationToken)
    {
        var maintenanceMode = await queries.IsMaintenanceModeEnabledAsync(cancellationToken);

        return Results.Ok(new StoreStatusResponse(maintenanceMode));
    }

    private sealed record StoreStatusResponse(bool MaintenanceMode);
}
