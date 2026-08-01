using Bojan.Api.Auth;
using Bojan.Domain.Admin;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace Bojan.Api.Endpoints;

/// <summary>
/// The policies every protected route names.
/// </summary>
/// <remarks>
/// <para>
/// The role policies mirror <c>apps/admin/src/lib/api/resources.ts</c> exactly.
/// The panel already refuses a write the operator's role does not cover;
/// <c>BACKEND.md</c> Phase 7 says to "enforce it again", and these are that
/// second enforcement — the one that still holds when a request skips the panel
/// entirely.
/// </para>
/// <para>
/// Every policy accepts both authentication schemes. Which one a caller used
/// does not change what they may do: the JWT and the trusted-proxy header
/// produce the same claims, and the <c>scope</c> claim is what keeps a customer
/// credential out of the admin policies.
/// </para>
/// </remarks>
public static class AuthorizationPolicies
{
    public const string Customer = "customer";

    /// <summary>Any signed-in operator, whatever their role — the <c>ALL</c> row in the panel's own table.</summary>
    public const string Admin = "admin";

    /// <summary><c>owner</c> only — settings, backups, API keys, the audit trail.</summary>
    public const string AdminOwner = "admin:owner";

    /// <summary><c>owner, product</c> — the catalogue and content screens.</summary>
    public const string AdminCatalogue = "admin:catalogue";

    /// <summary><c>owner, sales</c> — coupons, B2B, broadcasts.</summary>
    public const string AdminSales = "admin:sales";

    /// <summary><c>owner, support</c> — support threads and canned replies.</summary>
    public const string AdminSupport = "admin:support";

    /// <summary><c>owner, sales, support</c> — the order status control.</summary>
    public const string AdminOrders = "admin:orders";

    private static readonly string[] BothSchemes =
        [JwtBearerDefaults.AuthenticationScheme, TrustedProxyOptions.SchemeName];

    public static void AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Customer, policy => policy
                .AddAuthenticationSchemes(BothSchemes)
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "customer"))
            .AddPolicy(Admin, policy => Operator(policy))
            .AddPolicy(AdminOwner, policy => Operator(policy).RequireRole(Role(AdminRole.Owner)))
            .AddPolicy(AdminCatalogue, policy => Operator(policy)
                .RequireRole(Role(AdminRole.Owner), Role(AdminRole.Product)))
            .AddPolicy(AdminSales, policy => Operator(policy)
                .RequireRole(Role(AdminRole.Owner), Role(AdminRole.Sales)))
            .AddPolicy(AdminSupport, policy => Operator(policy)
                .RequireRole(Role(AdminRole.Owner), Role(AdminRole.Support)))
            .AddPolicy(AdminOrders, policy => Operator(policy)
                .RequireRole(Role(AdminRole.Owner), Role(AdminRole.Sales), Role(AdminRole.Support)));
    }

    private static AuthorizationPolicyBuilder Operator(AuthorizationPolicyBuilder policy) => policy
        .AddAuthenticationSchemes(BothSchemes)
        .RequireAuthenticatedUser()
        .RequireClaim("scope", "admin");

    /// <summary>The lowercase spelling the frontend's <c>AdminRole</c> union uses.</summary>
    private static string Role(AdminRole role) => role.ToString().ToLowerInvariant();
}
