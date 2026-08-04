using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Bojan.Api.Tests;

/// <summary>
/// Verifies the per-IP limits in <c>Endpoints/RateLimitPolicies.cs</c> — the
/// authoritative layer behind the frontend's own in-process limiter (see
/// <c>BACKEND.md</c> section 1.3 and that file's remarks).
/// </summary>
/// <remarks>
/// One shared factory across every test here, deliberately — a fresh one per
/// test would give each its own limiter state and never actually exhaust it.
/// </remarks>
public sealed class RateLimitingTests : IClassFixture<BojanApiFactory>
{
    private readonly BojanApiFactory _factory;

    public RateLimitingTests(BojanApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Otp_request_is_rejected_after_five_calls_per_minute()
    {
        using var client = _factory.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
        {
            last = await client.PostAsJsonAsync("/api/auth/otp/request", new { phone = "09121119000" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    /// <summary>
    /// The sign-in limit, on its own host so the factory's test-wide override
    /// is not the thing being measured.
    /// </summary>
    /// <remarks>
    /// The second factor's exchange shares this policy on purpose — the two
    /// calls are halves of one attempt, and giving <c>/auth/2fa</c> a window of
    /// its own would mean a six-digit code could be guessed at a rate the
    /// password step would never allow.
    /// </remarks>
    [Fact]
    public async Task Admin_sign_in_is_rejected_after_the_configured_number_of_attempts()
    {
        using var factory = new ShippedLimitsFactory();
        factory.EnsureDatabaseCreated();
        using var client = factory.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < ShippedAdminLoginAttempts + 1; i++)
        {
            last = await client.PostAsJsonAsync(
                "/api/admin/auth/login",
                new { identity = "nobody@bojan.example", password = "not-a-real-password" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    /// <summary>Mirrors the panel's own <c>LOGIN_MAX_ATTEMPTS</c>.</summary>
    private const int ShippedAdminLoginAttempts = 8;

    /// <summary>The shared factory with the sign-in limit put back to what ships.</summary>
    private sealed class ShippedLimitsFactory : BojanApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RateLimits:AdminLogin:PermitLimit"] = ShippedAdminLoginAttempts.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    ["RateLimits:AdminLogin:WindowSeconds"] = "300",
                }));
        }
    }
}
