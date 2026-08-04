using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Bojan.Application.Auth;
using Bojan.Domain.Admin;
using Microsoft.Extensions.Options;
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

    public string GenerateCustomerToken(Guid customerId, string phone, Guid securityStamp) => Generate(
        _options.CustomerTokenLifetime,
        [
            new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString()),
            new Claim("phone", phone),
            new Claim("scope", "customer"),
            new Claim(CustomerSessionClaims.SecurityStamp, securityStamp.ToString()),
        ]);

    public string GenerateAdminToken(Guid adminId, AdminRole role) => Generate(
        _options.AdminTokenLifetime,
        [
            new Claim(JwtRegisteredClaimNames.Sub, adminId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString().ToLowerInvariant()),
            new Claim("scope", "admin"),
        ]);

    /// <summary>
    /// The half-signed-in state, as a token that names one operator and
    /// authorises nothing.
    /// </summary>
    /// <remarks>
    /// Its <c>scope</c> is <c>admin-2fa</c>, not <c>admin</c>, so none of the
    /// authorisation policies accept it — they all require <c>scope</c> to be
    /// exactly <c>admin</c> or <c>customer</c>. The only code that reads it is
    /// <see cref="ReadTwoFactorChallenge"/>.
    /// </remarks>
    public string GenerateTwoFactorChallenge(Guid adminId) => Generate(
        _options.TwoFactorChallengeLifetime,
        [
            new Claim(JwtRegisteredClaimNames.Sub, adminId.ToString()),
            new Claim("scope", TwoFactorScope),
        ]);

    public Guid? ReadTwoFactorChallenge(string? challenge)
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

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(challenge, parameters, out _);

            // Checked rather than assumed: without it a full admin session
            // token would also satisfy this method, and the second factor would
            // be skippable by anyone holding one.
            if (principal.FindFirst("scope")?.Value != TwoFactorScope)
            {
                return null;
            }

            var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return Guid.TryParse(subject, out var adminId) ? adminId : null;
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            // Forged, expired, or not a token at all. All three are the same
            // answer to the caller: no operator.
            return null;
        }
    }

    private const string TwoFactorScope = "admin-2fa";

    private string Generate(TimeSpan lifetime, Claim[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now + lifetime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
