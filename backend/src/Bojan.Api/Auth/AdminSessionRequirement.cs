using System.Security.Claims;
using Bojan.Application.Auth;
using Bojan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Auth;

/// <summary>
/// The operator's session must still be one the account recognises.
/// </summary>
/// <remarks>
/// <para>
/// The panel's own credential is checked where it arrives — the trusted-proxy
/// handler has the row in hand for the role, so comparing the stamp there is
/// free, and a mismatch fails authentication outright. This closes the other
/// door: a bearer token, which nothing in the panel uses today but which the
/// API issues and accepts, and which would otherwise stay good for its whole
/// lifetime after the password behind it changed.
/// </para>
/// <para>
/// It is a requirement on the policies rather than a check inside the JWT
/// handler so that adding a third way in cannot quietly skip it.
/// </para>
/// </remarks>
public sealed class AdminSessionRequirement : IAuthorizationRequirement;

public sealed class AdminSessionHandler(BojanDbContext db)
    : AuthorizationHandler<AdminSessionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminSessionRequirement requirement)
    {
        // A customer principal reaching a policy that names this is not what it
        // is about; the policy's own scope claim has already decided that.
        if (context.User.FindFirstValue("scope") != "admin")
        {
            context.Succeed(requirement);
            return;
        }

        // Already compared against the row the scheme loaded to read the role.
        // Repeating it here would be a second keyed read on every panel request
        // for an answer that cannot have changed since.
        if (context.User.Identity?.AuthenticationType == TrustedProxyOptions.SchemeName)
        {
            context.Succeed(requirement);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId)
            || !Guid.TryParse(context.User.FindFirstValue(AdminSessionClaims.SecurityStamp), out var presented))
        {
            context.Fail();
            return;
        }

        var current = await db.AdminUsers
            .Where(candidate => candidate.Id == adminId && candidate.IsActive)
            .Select(candidate => (Guid?)candidate.SecurityStamp)
            .FirstOrDefaultAsync();

        if (current is null || current != presented)
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }
}
