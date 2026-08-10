using Bojan.Api.Auth;
using Bojan.Application.Contracts;
using Bojan.Application.Notifications;
using Bojan.Domain.Admin;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Which SMS service the shop sends through — the panel's SMS settings screen.
/// </summary>
/// <remarks>
/// Owner only. The API key spends the shop's SMS credit and can send anything
/// in the shop's name to any number, which is not a thing an operator trusted
/// with the order queue is thereby trusted with.
/// </remarks>
public static class SmsSettingsEndpoints
{
    public static void MapSmsSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sms")
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Settings)
            .NoStore();

        group.MapGet("/settings", GetSettings);

        group.MapPost("/settings", SaveSettings)
            .RequireRateLimiting(RateLimitPolicies.AdminWrite);

        // Its own ceiling rather than the shared admin-write one: each press
        // sends a real message and spends real credit.
        group.MapPost("/settings/test", TestSend)
            .RequireRateLimiting(RateLimitPolicies.PaymentCallback);
    }

    private static async Task<IResult> GetSettings(
        SmsSettingsService sms,
        CancellationToken cancellationToken) =>
        Results.Ok(await sms.GetAsync(cancellationToken));

    private static async Task<IResult> SaveSettings(
        SaveSmsSettingsRequest body,
        SmsSettingsService sms,
        CancellationToken cancellationToken) =>
        ApiResults.From(await sms.SaveAsync(body, cancellationToken));

    /// <summary>
    /// Sends one real message and reports what the provider said.
    /// </summary>
    /// <remarks>
    /// Always 200, with the outcome in the body — a failure here is the
    /// operator's configuration, and the sentence explaining which is the whole
    /// point of the button. See <see cref="PaymentSettingsEndpoints"/> for the
    /// same reasoning.
    /// </remarks>
    private static async Task<IResult> TestSend(
        TestSmsRequest body,
        SmsSettingsService sms,
        CancellationToken cancellationToken) =>
        Results.Ok(await sms.TestAsync(body.Phone, cancellationToken));
}
