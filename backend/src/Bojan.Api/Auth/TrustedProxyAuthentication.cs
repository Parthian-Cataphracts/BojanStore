using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Bojan.Application.Auth;
using Bojan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bojan.Api.Auth;

public sealed class TrustedProxyOptions : AuthenticationSchemeOptions
{
    public const string SectionName = "TrustedProxy";
    public const string SchemeName = "TrustedProxy";

    /// <summary>
    /// Shared secret the Next.js server sends as <c>X-Api-Key</c>. No default —
    /// unset means the scheme authenticates nobody.
    /// </summary>
    public string? ApiKey { get; set; }

    public const string ApiKeyHeader = "X-Api-Key";

    /// <summary>Header the storefront proxy already sends — see <c>apps/storefront/src/app/api/account/[action]/route.ts</c>.</summary>
    public const string CustomerHeader = "X-Customer-Id";

    /// <summary>Header the panel proxy already sends — see <c>apps/admin/src/app/api/admin/[resource]/route.ts</c>.</summary>
    public const string AdminHeader = "X-Admin-User";
}

/// <summary>
/// The second of the API's two ways in: a trusted server asserting who its
/// caller is.
/// </summary>
/// <remarks>
/// <para>
/// <c>BACKEND.md</c> section 1.3 leaves this as the one decision to make and
/// write down: "(a) shared secret header" or "(b) backend-issued JWT". Both are
/// implemented, and the reason is the shipped frontend rather than
/// indecision — the panel and the storefront's write proxy already send
/// <c>X-Admin-User</c> and <c>X-Customer-Id</c> and have no token to forward,
/// while (b) is what a mobile client would use later. JWT is the primary
/// scheme; this one exists so today's frontend works unchanged.
/// </para>
/// <para>
/// The identity headers are honoured <em>only</em> when <c>X-Api-Key</c>
/// matches the configured secret, compared in fixed time. That is what keeps
/// this from being "the API trusts whatever id you send": without the secret,
/// the headers authenticate nobody. It still means the API trusts the Next
/// server completely, which is exactly what option (a) says it does — and why
/// the key must never be given to anything running in a browser.
/// </para>
/// <para>
/// With no <see cref="TrustedProxyOptions.ApiKey"/> configured the scheme is
/// inert. There is no default that works, matching this solution's treatment of
/// <c>Jwt:SigningKey</c> and the frontend's of <c>ADMIN_DEV_PASSWORD</c>.
/// </para>
/// </remarks>
public sealed class TrustedProxyAuthenticationHandler(
    IOptionsMonitor<TrustedProxyOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    BojanDbContext db) : AuthenticationHandler<TrustedProxyOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configured = Options.ApiKey;
        if (string.IsNullOrEmpty(configured))
        {
            return AuthenticateResult.NoResult();
        }

        var presented = Request.Headers[TrustedProxyOptions.ApiKeyHeader].FirstOrDefault();
        if (string.IsNullOrEmpty(presented))
        {
            return AuthenticateResult.NoResult();
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(presented),
                Encoding.UTF8.GetBytes(configured)))
        {
            // Deliberately vague, and logged rather than returned: a caller who
            // learns *which* part was wrong learns whether the key is close.
            Logger.LogWarning("Rejected a request presenting an incorrect {Header}.", TrustedProxyOptions.ApiKeyHeader);
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var customerHeader = Request.Headers[TrustedProxyOptions.CustomerHeader].FirstOrDefault();
        var adminHeader = Request.Headers[TrustedProxyOptions.AdminHeader].FirstOrDefault();

        // One identity per request. Neither proxy ever sends both — each knows
        // only its own session — so a request carrying both is not a shape this
        // system produces. Refusing it keeps the scope claim single-valued,
        // which is what the policies and CurrentUser both assume: two `scope`
        // claims would satisfy the customer *and* the admin policy at once, and
        // `FindFirstValue` would pick whichever landed first.
        if (customerHeader is not null && adminHeader is not null)
        {
            Logger.LogWarning(
                "Rejected a request presenting both {Customer} and {Admin}.",
                TrustedProxyOptions.CustomerHeader,
                TrustedProxyOptions.AdminHeader);
            return AuthenticateResult.Fail("Ambiguous identity headers.");
        }

        var claims = new List<Claim>();

        if (Guid.TryParse(customerHeader, out var customerId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, customerId.ToString()));
            claims.Add(new Claim("scope", "customer"));

            // Forwarded, not verified here: the comparison against the account
            // is CustomerSessionRequirement's, so both schemes are checked by
            // one piece of code rather than each by its own.
            var stamp = Request.Headers[CustomerSessionClaims.StampHeader].FirstOrDefault();
            if (!string.IsNullOrEmpty(stamp))
            {
                claims.Add(new Claim(CustomerSessionClaims.SecurityStamp, stamp));
            }
        }

        if (Guid.TryParse(adminHeader, out var adminId))
        {
            // The role is read from this database, never from a header. The
            // proxy is trusted to say *who* is calling — it has verified its
            // own session cookie — but what that operator may do is a fact this
            // API owns, and a header carrying "owner" would make the role gates
            // decorative. An inactive or unknown operator authenticates as
            // nobody.
            var admin = await db.AdminUsers
                .Where(candidate => candidate.Id == adminId && candidate.IsActive)
                .Select(candidate => new { candidate.Id, candidate.Role, candidate.SecurityStamp })
                .FirstOrDefaultAsync();

            if (admin is not null)
            {
                // Compared here rather than in a policy requirement, unlike the
                // customer stamp: the row is already loaded for the role, so
                // this costs nothing, where a requirement would read it twice.
                // A session minted before the column existed carries no header
                // and is refused — a credential that predates the revocation
                // check is exactly the one revocation cannot reach.
                var presentedStamp = Request.Headers[AdminSessionClaims.StampHeader].FirstOrDefault();

                if (!Guid.TryParse(presentedStamp, out var stamp) || stamp != admin.SecurityStamp)
                {
                    Logger.LogInformation(
                        "Refused an operator session whose {Header} no longer matches the account.",
                        AdminSessionClaims.StampHeader);
                    return AuthenticateResult.Fail("Stale operator session.");
                }

                claims.Add(new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()));
                claims.Add(new Claim(ClaimTypes.Role, admin.Role.ToString().ToLowerInvariant()));
                claims.Add(new Claim("scope", "admin"));
                claims.Add(new Claim(AdminSessionClaims.SecurityStamp, stamp.ToString()));
            }
        }

        if (claims.Count == 0)
        {
            // A valid key with no usable identity header is a service call, not
            // a user one. Nothing in this API is reachable that way, so it
            // authenticates as nobody rather than as everybody.
            return AuthenticateResult.NoResult();
        }

        var identity = new ClaimsIdentity(claims, TrustedProxyOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, TrustedProxyOptions.SchemeName));
    }
}
