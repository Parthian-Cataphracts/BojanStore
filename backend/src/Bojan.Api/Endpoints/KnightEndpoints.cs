using Bojan.Api.Auth;
using Bojan.Api.Knight;
using Bojan.Domain.Admin;
using Knight.StoreAgent;

namespace Bojan.Api.Endpoints;

/// <summary>
/// The shop's connection to KNIGHT — the panel's «اتصال به نایت» screen.
/// </summary>
/// <remarks>
/// Owner only, and it is one of the clearest cases for that: the credential
/// entered here lets a control plane install code and configuration into this
/// shop. An operator trusted with the order queue is not thereby trusted with
/// that.
///
/// Nothing on this surface returns a secret. The screen answers "is this
/// connected, when did it last work, and what has been delivered", and every one
/// of those is answerable without one — which is what makes the screen safe to
/// look at over somebody's shoulder.
/// </remarks>
public static class KnightEndpoints
{
    public static void MapKnightEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/knight")
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Settings)
            .NoStore();

        group.MapGet("/status", GetStatus);

        group.MapPost("/connect", Connect)
            .RequireRateLimiting(RateLimitPolicies.AdminWrite);

        group.MapPost("/disconnect", Disconnect)
            .RequireRateLimiting(RateLimitPolicies.AdminWrite);
    }

    private static async Task<IResult> GetStatus(
        KnightStatusReader status,
        CancellationToken cancellationToken)
    {
        var snapshot = await status.ReadAsync(cancellationToken);

        return Results.Ok(new KnightStatusResponse(
            snapshot.Configured,
            snapshot.Enabled,
            snapshot.Connected,
            snapshot.BaseUrl,
            snapshot.ClientId,
            snapshot.StoreId,
            snapshot.StoreName,
            snapshot.Slug,
            snapshot.IntegrationStatus,
            snapshot.LastHandshakeAt,
            snapshot.LastHeartbeatAt,
            snapshot.LastJobAt,
            snapshot.LastJob,
            snapshot.LastError,
            snapshot.LastErrorAt,
            KnightIntegration.ProxyBasePath,
            [.. snapshot.Features.Select(feature => new KnightFeatureResponse(
                feature.Slug,
                feature.Version,
                feature.Enabled,
                feature.Architecture,
                feature.HasServiceSecret,
                [.. feature.ProxyPrefixes.Select(prefix =>
                    $"{KnightIntegration.ProxyBasePath}/{prefix.TrimStart('/')}")],
                [.. feature.UiMounts.Select(mount => new KnightMountResponse(
                    mount.Slot, mount.Label, mount.Path, mount.Kind))]))]));
    }

    /// <summary>
    /// Records the credential KNIGHT issued for this shop and starts talking.
    /// </summary>
    /// <remarks>
    /// No restart. The agent reads the credential on its next pass, so a shop
    /// connected at ten past the hour is polling for work at eleven past —
    /// which is the whole reason this screen exists rather than an environment
    /// variable somebody else has to set.
    ///
    /// The secret is not checked here, because nothing local can check it. What
    /// answers that question is the handshake, and the screen reports what it
    /// said. A panel that pretended to validate a credential would be telling an
    /// owner something it cannot know.
    /// </remarks>
    private static async Task<IResult> Connect(
        ConnectToKnightRequest body,
        KnightConnection connection,
        KnightStatusReader status,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.BaseUrl)
            || string.IsNullOrWhiteSpace(body.ClientId)
            || string.IsNullOrWhiteSpace(body.ClientSecret))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["credential"] = ["آدرس نایت، شناسه و کلید هر سه لازم‌اند."],
            });
        }

        if (!Uri.TryCreate(body.BaseUrl.Trim(), UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["baseUrl"] = ["آدرس نایت باید یک نشانی کامل http یا https باشد."],
            });
        }

        await connection.ConnectAsync(
            new KnightCredential
            {
                BaseUrl = parsed.ToString().TrimEnd('/'),
                ClientId = body.ClientId.Trim(),
                ClientSecret = body.ClientSecret.Trim(),
                Environment = string.IsNullOrWhiteSpace(body.Environment) ? "Production" : body.Environment.Trim(),
            },
            cancellationToken);

        // The status as it is *now*, which is "configured and not yet
        // connected". The handshake happens on the agent's next pass and the
        // screen shows it when it does; claiming success here would be claiming
        // something that has not happened.
        return Results.Ok(await status.ReadAsync(cancellationToken));
    }

    /// <summary>
    /// Stops this shop talking to KNIGHT and forgets the credential.
    /// </summary>
    /// <remarks>
    /// Features already delivered stay installed and keep serving.
    /// Disconnecting is not uninstalling: what an owner means by it is "stop
    /// talking to them", and deleting a shop's paid-for Features because
    /// somebody pressed the wrong button is not recoverable from this screen.
    /// </remarks>
    private static async Task<IResult> Disconnect(
        KnightConnection connection,
        KnightStatusReader status,
        CancellationToken cancellationToken)
    {
        await connection.DisconnectAsync(cancellationToken);

        return Results.Ok(await status.ReadAsync(cancellationToken));
    }

    private sealed record ConnectToKnightRequest(
        string BaseUrl,
        string ClientId,
        string ClientSecret,
        string? Environment);

    private sealed record KnightStatusResponse(
        bool Configured,
        bool Enabled,
        bool Connected,
        string BaseUrl,
        string ClientId,
        string StoreId,
        string StoreName,
        string Slug,
        string IntegrationStatus,
        DateTimeOffset? LastHandshakeAt,
        DateTimeOffset? LastHeartbeatAt,
        DateTimeOffset? LastJobAt,
        string LastJob,
        string LastError,
        DateTimeOffset? LastErrorAt,
        string ProxyBasePath,
        IReadOnlyList<KnightFeatureResponse> Features);

    private sealed record KnightFeatureResponse(
        string Slug,
        string Version,
        bool Enabled,
        string Architecture,
        bool HasServiceSecret,
        IReadOnlyList<string> Routes,
        IReadOnlyList<KnightMountResponse> Mounts);

    private sealed record KnightMountResponse(string Slot, string Label, string Path, string Kind);
}
