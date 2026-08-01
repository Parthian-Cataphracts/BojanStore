using System.Net;
using System.Net.Http.Json;

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
}
