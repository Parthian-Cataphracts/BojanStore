using Bojan.Application.Common;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Auth;

/// <summary>
/// Enforces screen 146's permission grid.
/// </summary>
/// <remarks>
/// <para>
/// The grid was stored and read back onto its own screen and consulted by
/// nothing else: an owner could withdraw "پشتیبانی" from the sales role, the
/// save would succeed, the grid would show it withdrawn, and that role's access
/// was exactly what it had been. A screen that reports a permission as revoked
/// while it is not is worse than not having the screen.
/// </para>
/// <para>
/// This is the second gate, not the first. The role policies on each route
/// (<see cref="Endpoints.AuthorizationPolicies"/>) still decide what a role may
/// ever reach; the grid can only narrow that, never widen it. So revoking a
/// section takes access away and ticking one the role's policy does not admit
/// gives nothing.
/// </para>
/// <para>
/// <c>owner</c> is never checked. A panel whose full-access role can be locked
/// out of settings is one save away from being unadministrable, which is also
/// why the grid draws that row locked and the service refuses to store it.
/// </para>
/// </remarks>
public sealed class SectionPermissionFilter(string section) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var user = services.GetRequiredService<ICurrentUser>();

        // Not an operator, or the owner: nothing here applies. The route's own
        // policy has already decided whether they may be here at all.
        if (user.AdminId is null || string.Equals(user.AdminRole, "owner", StringComparison.Ordinal))
        {
            return await next(context);
        }

        var role = user.AdminRole;
        if (string.IsNullOrEmpty(role))
        {
            return await next(context);
        }

        var db = services.GetRequiredService<BojanDbContext>();

        // An installation that has never opened screen 146 has no rows at all,
        // and must keep behaving exactly as it did — the grid narrows the role
        // policies, so "not yet configured" has to mean "whatever the policies
        // already allow" rather than "nothing".
        if (!await db.RolePermissions.AsNoTracking().AnyAsync(context.HttpContext.RequestAborted))
        {
            return await next(context);
        }

        var granted = await db.RolePermissions.AsNoTracking()
            .AnyAsync(
                grant => grant.Role == role && grant.Section == section,
                context.HttpContext.RequestAborted);

        if (!granted)
        {
            // Forbidden, not not-found: the operator is signed in and the
            // resource plainly exists — what is missing is a permission their
            // own owner can grant, and saying so is how they get it.
            return Endpoints.ApiResults.Problem(UseCaseError.Forbidden, "section-not-granted");
        }

        return await next(context);
    }
}

public static class SectionPermissionExtensions
{
    /// <summary>Requires the caller's role to hold <paramref name="section"/> on screen 146's grid.</summary>
    public static RouteHandlerBuilder RequireSection(this RouteHandlerBuilder builder, string section) =>
        builder.AddEndpointFilter(new SectionPermissionFilter(section));

    /// <summary>The same, for a whole group.</summary>
    public static RouteGroupBuilder RequireSection(this RouteGroupBuilder builder, string section) =>
        builder.AddEndpointFilter(new SectionPermissionFilter(section));
}
