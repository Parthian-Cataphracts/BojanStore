using Bojan.Api.Auth;
using Bojan.Application.Contracts;
using Bojan.Application.Payments;
using Bojan.Domain.Admin;

namespace Bojan.Api.Endpoints;

/// <summary>
/// Which gateway the shop is pointed at — the panel's payment settings screen.
/// </summary>
/// <remarks>
/// <para>
/// Owner only, all three. The merchant id decides whose account customers' money
/// lands in, and someone trusted to work the order queue is not thereby trusted
/// to change that. This is the same line the mailbox settings draw for the same
/// reason.
/// </para>
/// <para>
/// The screen previously wrote into the generic settings table, where nothing
/// read it: choosing a gateway and typing a merchant id changed the text on the
/// form and nothing else. These routes are what make that screen the thing it
/// claimed to be.
/// </para>
/// </remarks>
public static class PaymentSettingsEndpoints
{
    public static void MapPaymentSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payment")
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Settings)
            .NoStore();

        group.MapGet("/settings", GetSettings);

        group.MapPost("/settings", SaveSettings)
            .RequireRateLimiting(RateLimitPolicies.AdminWrite);

        // Its own ceiling rather than the shared admin-write one: each press is
        // a payment request against the shop's real terminal, and ZarinPal
        // answers -12 to a burst of them.
        group.MapPost("/settings/test", TestConnection)
            .RequireRateLimiting(RateLimitPolicies.PaymentCallback);
    }

    /// <summary>
    /// The shipping tiers, which had no panel surface at all.
    /// </summary>
    /// <remarks>
    /// Mapped from here because it is the same kind of thing as the payment
    /// methods above — a settings screen that named figures the checkout was
    /// never reading. Owner only, like the rest of the settings section.
    /// </remarks>
    public static void MapShippingSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/shipping")
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Settings)
            .NoStore();

        group.MapGet("/methods", ListShippingMethods);

        group.MapPost("/methods", SaveShippingMethods)
            .RequireRateLimiting(RateLimitPolicies.AdminWrite);
    }

    /// <summary>
    /// The loyalty club — its tiers and what a point costs to earn.
    /// </summary>
    /// <remarks>
    /// Owner only, beside shipping and payment and for the same reason: a tier
    /// sets a standing discount on every order every member ever places, which
    /// is the shop's margin rather than one campaign's.
    ///
    /// The read is public and lives on the storefront's own routes — the club is
    /// how the shop recruits, so its tiers have to be visible to someone who has
    /// not joined. Only the write is here.
    /// </remarks>
    public static void MapLoyaltyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/loyalty")
            .RequireAuthorization(AuthorizationPolicies.AdminOwner)
            .RequireSection(PanelSection.Settings)
            .NoStore();

        group.MapGet(string.Empty, GetLoyalty);

        group.MapPost(string.Empty, SaveLoyalty)
            .RequireRateLimiting(RateLimitPolicies.AdminWrite);
    }

    private static async Task<IResult> GetLoyalty(
        Application.Accounts.LoyaltyService loyalty,
        CancellationToken cancellationToken) =>
        Results.Ok(await loyalty.GetAsync(cancellationToken));

    private static async Task<IResult> SaveLoyalty(
        SaveLoyaltyRequest body,
        Application.Accounts.LoyaltyService loyalty,
        CancellationToken cancellationToken) =>
        ApiResults.From(await loyalty.SaveAsync(body, cancellationToken));

    private static async Task<IResult> ListShippingMethods(
        Application.Administration.ShippingSettingsService shipping,
        CancellationToken cancellationToken) =>
        Results.Ok(await shipping.ListAsync(cancellationToken));

    private static async Task<IResult> SaveShippingMethods(
        SaveShippingMethodsRequest body,
        Application.Administration.ShippingSettingsService shipping,
        CancellationToken cancellationToken) =>
        ApiResults.From(await shipping.SaveAsync(body, cancellationToken));

    private static async Task<IResult> GetSettings(
        PaymentSettingsService payments,
        CancellationToken cancellationToken) =>
        Results.Ok(await payments.GetAsync(cancellationToken));

    private static async Task<IResult> SaveSettings(
        SavePaymentSettingsRequest body,
        PaymentSettingsService payments,
        CancellationToken cancellationToken) =>
        ApiResults.From(await payments.SaveAsync(body, cancellationToken));

    /// <summary>
    /// Asks the configured provider whether these credentials work.
    /// </summary>
    /// <remarks>
    /// Always 200, with the outcome in the body. A failure here is the
    /// operator's configuration rather than a fault in this API, and the
    /// sentence explaining which is the entire point of the button — a status
    /// code cannot carry "the callback address does not match the domain
    /// registered on your terminal".
    /// </remarks>
    private static async Task<IResult> TestConnection(
        PaymentSettingsService payments,
        CancellationToken cancellationToken) =>
        Results.Ok(await payments.TestAsync(cancellationToken));
}
