using System.Security.Claims;
using Bojan.Domain.Admin;
using Knight.StoreAgent;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bojan.Api.Knight;

/// <summary>
/// This shop's half of the KNIGHT integration.
///
/// The agent itself is a vendored library shared by every ASP.NET Core store on
/// that platform (<c>backend/vendor/Knight.StoreAgent</c>). What belongs here is
/// only the part that is about **this** shop: which events it publishes, where a
/// delivered screen may hang, and how its own roles map onto the three
/// identities a Feature's service is allowed to be told about.
///
/// Nothing else about the platform reaches into the rest of the application. The
/// storefront and the panel do not know KNIGHT exists; what they see is a
/// settings screen and, once something has been delivered, a route that answers.
/// </summary>
public static class KnightIntegration
{
    /// <summary>
    /// Where this shop's URL space is handed over to a Feature's service.
    ///
    /// Under the API's own prefix and behind a segment of its own, rather than
    /// at the root. A manifest declares a prefix like <c>subscriptions/</c> "in
    /// the store's own URL space", and the store decides where that space
    /// begins — putting it at the root would let a delivered configuration
    /// shadow a route this shop already serves, and the shop would find out from
    /// a customer.
    /// </summary>
    public const string ProxyBasePath = "/api/features";

    public static IServiceCollection AddKnightIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // What this shop actually publishes and offers. The agent checks a
        // delivered configuration against these at install, because KNIGHT can
        // only check the shape of an event name and this is the only place that
        // knows the names — without it a Feature subscribing to `order.plaecd`
        // installs cleanly and never hears anything.
        StoreEventCatalogue.KnownEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            "order.placed",
            "order.paid",
            "order.cancelled",
            "order.refunded",
            "order.fulfilled",
            "cart.abandoned",
            "customer.registered",
            "customer.updated",
            "product.created",
            "product.updated",
            "product.stock_changed",
        };

        StoreEventCatalogue.UiSlots = new HashSet<string>(StringComparer.Ordinal)
        {
            "admin.sidebar",
            "admin.order_detail",
            "admin.customer_detail",
            "admin.settings",
            "storefront.account",
        };

        services.AddKnightStoreAgent(configuration);

        // This shop's own answer to "who is asking". Registered before the
        // library's default is added, because the default reads role names this
        // application does not use.
        services.TryAddSingleton<IKnightProxyIdentity, BojanProxyIdentity>();

        return services;
    }
}

/// <summary>
/// Who this shop is prepared to say a request is from.
///
/// Three answers and no more, because they are the three a Feature's service is
/// allowed to be told: nobody, a shopper, or a member of staff. The service
/// never sees a cookie or a token — the assertion below is the whole of the
/// identity that crosses, and it is signed.
///
/// The <c>scope</c> claim is what separates a customer session from an operator
/// one in this application, so it is what separates them here. Reading the role
/// alone would let a customer credential with a role claim from somewhere else
/// present itself as staff.
/// </summary>
public sealed class BojanProxyIdentity : IKnightProxyIdentity
{
    public (string Identity, string Subject) Describe(HttpContext context)
    {
        var user = context.User;

        if (user?.Identity is not { IsAuthenticated: true })
        {
            return ("anonymous", string.Empty);
        }

        var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? string.Empty;

        var scope = user.FindFirst("scope")?.Value;

        if (string.Equals(scope, "admin", StringComparison.Ordinal))
        {
            return ("staff", subject);
        }

        return string.Equals(scope, "customer", StringComparison.Ordinal)
            ? ("customer", subject)
            // A signed-in principal whose scope is neither is not somebody this
            // shop can describe, and describing them wrongly is how a shopper
            // reaches a merchant's screens.
            : ("anonymous", string.Empty);
    }
}
