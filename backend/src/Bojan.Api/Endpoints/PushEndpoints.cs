using Bojan.Api.Auth;
using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Application.Notifications;
using Bojan.Domain.Admin;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Browser notifications: what a visitor needs to subscribe, and the owner's
/// settings behind it.
/// </summary>
/// <remarks>
/// Split across the two base paths on purpose. Whether push is on and what the
/// shop's public key is has to be readable by anyone who might subscribe — it is
/// published material, and hiding it would only stop the storefront working. The
/// key pair itself sits under <c>/api/admin</c> and never leaves the server.
/// </remarks>
public static class PushEndpoints
{
    public static void MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous and cacheable-by-nobody: a visitor who has not signed in
        // still renders the page that decides whether to offer the prompt, and
        // the answer changes the moment the owner switches push on.
        app.MapGroup("/push").NoStore().MapGet("/availability", GetAvailability);

        var mine = app.MapGroup("/me/push")
            .RequireAuthorization(AuthorizationPolicies.Customer)
            .NoStore();

        // Under the public-write ceiling. Each call is one row, but an
        // unbounded one would let a client mint subscription rows as fast as it
        // can post — and a browser subscribing legitimately does it once.
        mine.MapPost("/subscribe", Subscribe).RequireRateLimiting(RateLimitPolicies.PublicWrite);
        mine.MapPost("/unsubscribe", Unsubscribe).RequireRateLimiting(RateLimitPolicies.PublicWrite);
    }

    public static void MapPushSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/push")
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Settings)
            .NoStore();

        group.MapGet("/settings", GetSettings);

        group.MapPost("/settings", SaveSettings)
            .RequireRateLimiting(RateLimitPolicies.AdminWrite);

        // Its own route rather than a field on the save, because it is not a
        // setting — it disconnects every browser subscribed under the old key,
        // and that is not something to do as a side effect of pressing save.
        group.MapPost("/settings/keys", GenerateKeys)
            .RequireRateLimiting(RateLimitPolicies.AdminWrite);
    }

    private static async Task<IResult> GetAvailability(
        PushSubscriptionService push,
        CancellationToken cancellationToken) =>
        Results.Ok(await push.GetAvailabilityAsync(cancellationToken));

    /// <remarks>
    /// The user agent is read from the request rather than taken from the body:
    /// it is only there so a customer can tell one of their devices from
    /// another, and a field the page fills in would be a field the page can
    /// write anything into.
    /// </remarks>
    private static async Task<IResult> Subscribe(
        SavePushSubscriptionRequest body,
        HttpContext http,
        PushSubscriptionService push,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        ApiResults.From(await push.SubscribeAsync(
            user.CustomerId!.Value,
            body,
            http.Request.Headers.UserAgent.ToString(),
            cancellationToken));

    private static async Task<IResult> Unsubscribe(
        UnsubscribePushRequest body,
        PushSubscriptionService push,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        ApiResults.From(await push.UnsubscribeAsync(
            user.CustomerId!.Value,
            body.Endpoint,
            cancellationToken));

    private static async Task<IResult> GetSettings(
        WebPushSettingsService push,
        CancellationToken cancellationToken) =>
        Results.Ok(await push.GetAsync(cancellationToken));

    private static async Task<IResult> SaveSettings(
        SaveWebPushSettingsRequest body,
        WebPushSettingsService push,
        CancellationToken cancellationToken) =>
        ApiResults.From(await push.SaveAsync(body, cancellationToken));

    private static async Task<IResult> GenerateKeys(
        WebPushSettingsService push,
        CancellationToken cancellationToken) =>
        Results.Ok(await push.GenerateKeysAsync(cancellationToken));
}

/// <summary>Which browser to forget — the endpoint it is reachable at.</summary>
public sealed record UnsubscribePushRequest(string Endpoint);
