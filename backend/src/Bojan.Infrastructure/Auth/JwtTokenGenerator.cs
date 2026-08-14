using System.Security.Claims;
using System.Text;
using Bojan.Application.Auth;
using Bojan.Domain.Admin;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bojan.Infrastructure.Auth;

/// <summary>
/// Issues the bearer token <c>BACKEND.md</c> section 1.3 recommends (option
/// b): the frontend's own session cookie stays exactly as it is — signed,
/// http-only, verified in its own middleware — and carries this token
/// forward as <c>Authorization: Bearer</c> on every subsequent call. This
/// class only ever mints a token for the customer or admin whose credential
/// was just verified; it never accepts an id from a request body.
/// </summary>
public sealed class JwtTokenGenerator(IOptions<JwtOptions> options) : IJwtTokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    /// <summary>
    /// One handler for the process, rather than one per token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="JsonWebTokenHandler"/> where this used to construct a
    /// <c>JwtSecurityTokenHandler</c> on every call. The newer handler is the
    /// one the JWT bearer middleware already validates incoming tokens with, so
    /// this makes both halves of the round trip the same implementation, and it
    /// is documented as thread-safe — which is what lets it be a static field
    /// instead of an allocation per sign-in.
    /// </para>
    /// <para>
    /// The claims below are written with the names they should have on the
    /// wire — <c>role</c>, not the schemas.microsoft.com URI — because this
    /// handler writes what it is given. The old one silently rewrote
    /// <see cref="ClaimTypes.Role"/> to <c>role</c> through its outbound map,
    /// and a token whose role claim was suddenly a URI would authorise nothing.
    /// </para>
    /// </remarks>
    private static readonly JsonWebTokenHandler Handler = new();

    public string GenerateCustomerToken(Guid customerId, string phone, Guid securityStamp) => Generate(
        _options.CustomerTokenLifetime,
        [
            new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString()),
            new Claim("phone", phone),
            new Claim("scope", "customer"),
            new Claim(CustomerSessionClaims.SecurityStamp, securityStamp.ToString()),
        ]);

    public string GenerateAdminToken(Guid adminId, AdminRole role, Guid securityStamp) => Generate(
        _options.AdminTokenLifetime,
        [
            new Claim(JwtRegisteredClaimNames.Sub, adminId.ToString()),
            new Claim("role", role.ToString().ToLowerInvariant()),
            new Claim("scope", "admin"),
            new Claim(AdminSessionClaims.SecurityStamp, securityStamp.ToString()),
        ]);

    /// <summary>
    /// The half-signed-in state, as a token that names one operator and
    /// authorises nothing.
    /// </summary>
    /// <remarks>
    /// Its <c>scope</c> is <c>admin-2fa</c>, not <c>admin</c>, so none of the
    /// authorisation policies accept it — they all require <c>scope</c> to be
    /// exactly <c>admin</c> or <c>customer</c>. The only code that reads it is
    /// <see cref="ReadTwoFactorChallengeAsync"/>.
    /// </remarks>
    public string GenerateTwoFactorChallenge(Guid adminId) => Generate(
        _options.TwoFactorChallengeLifetime,
        [
            new Claim(JwtRegisteredClaimNames.Sub, adminId.ToString()),
            new Claim("scope", TwoFactorScope),
        ]);

    public async Task<Guid?> ReadTwoFactorChallengeAsync(string? challenge)
    {
        if (string.IsNullOrWhiteSpace(challenge))
        {
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // Forged, expired, or not a token at all arrives as a false `IsValid`
        // rather than as an exception, which is why there is no catch here any
        // more. All three are still the same answer to the caller: no operator.
        var result = await Handler.ValidateTokenAsync(challenge, parameters);
        if (!result.IsValid || result.ClaimsIdentity is not { } identity)
        {
            return null;
        }

        // Checked rather than assumed: without it a full admin session token
        // would also satisfy this method, and the second factor would be
        // skippable by anyone holding one.
        if (identity.FindFirst("scope")?.Value != TwoFactorScope)
        {
            return null;
        }

        // Both names, because whether `sub` arrives mapped onto NameIdentifier
        // depends on a handler setting rather than on the token.
        var subject = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? identity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return Guid.TryParse(subject, out var adminId) ? adminId : null;
    }

    private const string TwoFactorScope = "admin-2fa";

    private string Generate(TimeSpan lifetime, Claim[] claims)
    {
        var now = DateTime.UtcNow;

        return Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            Expires = now + lifetime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        });
    }
}
