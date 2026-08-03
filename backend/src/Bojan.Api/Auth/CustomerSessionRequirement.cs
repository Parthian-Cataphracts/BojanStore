using System.Security.Claims;
using Bojan.Application.Auth;
using Bojan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Auth;

/// <summary>
/// The customer's session must still be one the account recognises.
/// </summary>
/// <remarks>
/// <para>
/// Both ways in are self-contained by design — a signed cookie and a signed
/// token, each carrying its own expiry, neither needing the database to be
/// checked. That is what made them impossible to revoke: changing a password
/// left every session open, which is the opposite of what the person changing
/// it wanted.
/// </para>
/// <para>
/// This is the one place that closes it, and it is a requirement on the policy
/// rather than a check inside either handler so that neither scheme can be the
/// one that forgot. It costs a keyed read per authorised customer request —
/// the same read the admin scheme has always paid to look a role up.
/// </para>
/// </remarks>
public sealed class CustomerSessionRequirement : IAuthorizationRequirement;

public sealed class CustomerSessionHandler(BojanDbContext db)
    : AuthorizationHandler<CustomerSessionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CustomerSessionRequirement requirement)
    {
        // Only customer principals carry a stamp. An operator reaching an
        // endpoint that names this requirement is not what it is about, and
        // failing them here would be a second, silent role check.
        if (context.User.FindFirstValue("scope") != "customer")
        {
            context.Succeed(requirement);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId)
            || !Guid.TryParse(context.User.FindFirstValue(CustomerSessionClaims.SecurityStamp), out var presented))
        {
            // No stamp at all is a session minted before this existed. It is
            // refused rather than allowed through: a credential that predates
            // the revocation check is exactly the one revocation cannot reach,
            // so those sessions end and their owners sign in once more.
            context.Fail();
            return;
        }

        var current = await db.Customers
            .Where(candidate => candidate.Id == customerId)
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
