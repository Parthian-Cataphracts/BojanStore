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

    /// <summary>
    /// A forged <c>X-Forwarded-For</c> does not buy a fresh window.
    /// </summary>
    /// <remarks>
    /// The limiter used to bucket on the left-most entry of that header, which
    /// is the one part of it the caller writes — proxies append rather than
    /// replace. Sending a different value per request therefore reset the
    /// window every time, which made this limit, and every other one in that
    /// file, a formality: unlimited sign-in codes, coupon guesses and tracking
    /// lookups from a single machine.
    /// </remarks>
    [Fact]
    public async Task A_forged_forwarding_header_does_not_reset_the_window()
    {
        using var client = _factory.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 12; i++)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/auth/otp/request")
            {
                Content = JsonContent.Create(new { phone = "09121119001" }),
            };

            // What the proxy in front actually produces: nginx's
            // $proxy_add_x_forwarded_for appends the peer it saw to whatever
            // arrived, so a forged value ends up on the left and the real one on
            // the right. Reading the left-most entry gave the caller a new
            // bucket per request; reading from the trusted end gives the same
            // one every time.
            message.Headers.Add("X-Forwarded-For", $"203.0.113.{i}, 198.51.100.7");

            last = await client.SendAsync(message);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }
}
