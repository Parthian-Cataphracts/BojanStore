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

        var db = services.GetRequiredService<BojanDbContext>();
        var adminId = user.AdminId.Value;

        /*
          Read per operator, and read live.

          It was per role: a grid of role against section, so granting one
          salesperson the returns queue granted it to every salesperson. The
          permission belonged to the job title rather than to the person, and
          there was no way to say «this one, not that one».

          Live, because the alternative is putting the sections in the session
          cookie — and a cookie lasts a working day, so an owner revoking access
          at nine would be revoking it at six.
        */
        var sections = db.AdminUserSections.AsNoTracking().Where(grant => grant.AdminUserId == adminId);

        // An operator nobody has narrowed keeps whatever their role's policies
        // already allow. "Not yet configured" has to mean that rather than
        // "nothing", or appointing somebody would lock them out of everything
        // until an owner went and ticked boxes.
        if (!await sections.AnyAsync(context.HttpContext.RequestAborted))
        {
            return await next(context);
        }

        var granted = await sections.AnyAsync(
            grant => grant.Section == section,
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
