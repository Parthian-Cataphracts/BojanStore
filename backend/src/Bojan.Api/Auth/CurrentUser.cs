using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Bojan.Application.Common;

namespace Bojan.Api.Auth;

/// <summary>
/// Who the caller is, read from the verified credential and from nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// <c>BACKEND.md</c> section 1.3's closing rule: "the API must never trust a
/// customer id that arrives in a request body. Derive it from the credential."
/// This class is where that derivation happens, and it is the only place any
/// handler asks who is calling — no endpoint reads an id parameter for
/// identity, because there is none to read.
/// </para>
/// <para>
/// The <c>scope</c> claim decides whether the subject is a customer or an
/// operator. Both schemes set it, so a customer token can never be presented at
/// an admin endpoint and be mistaken for an operator: the id would land in
/// <see cref="CustomerId"/>, and <see cref="AdminId"/> would stay null.
/// </para>
/// </remarks>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? CustomerId => SubjectFor("customer");

    public Guid? AdminId => SubjectFor("admin");

    public string? AdminRole => AdminId is null
        ? null
        : Principal?.FindFirstValue(ClaimTypes.Role) ?? Principal?.FindFirstValue("role");

    public string? Ip
    {
        get
        {
            var context = accessor.HttpContext;
            if (context is null)
            {
                return null;
            }

            // X-Forwarded-For is only trustworthy behind a proxy that
            // overwrites it — the same deployment assumption the rate limiter
            // and the frontend's own clientKey() already make.
            return context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                ?? context.Connection.RemoteIpAddress?.ToString();
        }
    }

    private Guid? SubjectFor(string scope)
    {
        var principal = Principal;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        if (principal.FindFirstValue("scope") != scope)
        {
            return null;
        }

        // JwtSecurityTokenHandler maps `sub` onto NameIdentifier by default,
        // but only when the inbound claim map is left alone — reading both
        // means this keeps working whichever way that mapping is configured.
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(subject, out var id) ? id : null;
    }
}
