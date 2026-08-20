using Bojan.Api.Auth;
using Bojan.Api.Contracts;
using Bojan.Application.Auth;
using Bojan.Domain.Admin;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Whether email and phone verification are required — the panel's
/// verification settings screen, owner only.
/// </summary>
/// <remarks>
/// Storage and reads only, matching <see cref="VerificationSettingsService"/>:
/// nothing here yet blocks sign-in or checkout on an unverified email or
/// phone.
/// </remarks>
public static class VerificationSettingsEndpoints
{
    public static void MapVerificationSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/settings/verification")
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Settings)
            .NoStore();

        group.MapGet(string.Empty, GetSettings);

        group.MapPut(string.Empty, SaveSettings)
            .RequireRateLimiting(RateLimitPolicies.AdminWrite);
    }

    private static async Task<IResult> GetSettings(
        VerificationSettingsService verification, CancellationToken cancellationToken) =>
        Results.Ok(await verification.GetAsync(cancellationToken));

    private static async Task<IResult> SaveSettings(
        VerificationSettingsBody body,
        VerificationSettingsService verification,
        CancellationToken cancellationToken) =>
        ApiResults.From(await verification.SaveAsync(
            new SaveVerificationSettingsRequest(body.RequireEmailVerification, body.RequirePhoneVerification),
            cancellationToken));
}
