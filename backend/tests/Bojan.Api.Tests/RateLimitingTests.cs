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

    /// <summary>
    /// A caller reaching the public catalogue directly is limited.
    /// </summary>
    /// <remarks>
    /// These reads had no ceiling at all, which made walking the catalogue free.
    /// The window is deliberately generous — see the policy — so this test
    /// lowers it rather than issuing three hundred requests to prove one thing.
    /// </remarks>
    [Fact]
    public async Task The_public_catalogue_is_limited_for_a_direct_caller()
    {
        using var factory = new CatalogueLimitFactory();
        factory.EnsureDatabaseCreated();
        using var client = factory.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
        {
            last = await client.GetAsync("/api/categories");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    /// <summary>
    /// The shop's own server is not.
    /// </summary>
    /// <remarks>
    /// Every page the storefront renders reads the catalogue server-side, and
    /// those calls all arrive from one address — the Next.js host. Counting them
    /// against a per-address ceiling is not a limit per shopper, it is a limit
    /// on the whole shop's rendering shared by everyone at once, and applying
    /// one took the site down: the layout alone reads the shipping methods on
    /// every page, so once the shared window filled, the sign-in and basket
    /// screens rendered the error boundary instead.
    ///
    /// Only the storefront and the panel hold the key — it is server-side
    /// configuration that never reaches a browser — so presenting it is what
    /// distinguishes the shop from a stranger.
    /// </remarks>
    [Fact]
    public async Task The_shops_own_server_is_not_limited_on_the_catalogue()
    {
        using var factory = new CatalogueLimitFactory();
        factory.EnsureDatabaseCreated();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", BojanApiFactory.TrustedProxyKey);

        // Well past the window the previous test exhausts in five.
        for (var i = 0; i < 25; i++)
        {
            var response = await client.GetAsync("/api/categories");
            response.EnsureSuccessStatusCode();
        }
    }

    /// <summary>A host whose catalogue window is small enough to exhaust in a test.</summary>
    private sealed class CatalogueLimitFactory : BojanApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RateLimits:CatalogueRead:PermitLimit"] = "4",
                    ["RateLimits:CatalogueRead:WindowSeconds"] = "60",
                }));
        }
    }
}
