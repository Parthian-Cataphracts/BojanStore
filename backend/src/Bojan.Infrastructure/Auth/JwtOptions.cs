namespace Bojan.Infrastructure.Auth;

/// <summary>
/// Bound from the <c>Jwt</c> section of configuration. See
/// <c>appsettings.Development.json</c> for the local defaults and
/// <c>appsettings.json</c> for why production has none.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    /// <summary>HMAC-SHA256 signing key. Must be at least 32 bytes — the same floor the frontend's own <c>AUTH_SECRET</c> enforces.</summary>
    public required string SigningKey { get; init; }

    public TimeSpan CustomerTokenLifetime { get; init; } = TimeSpan.FromDays(30);

    public TimeSpan AdminTokenLifetime { get; init; } = TimeSpan.FromHours(8);
}
