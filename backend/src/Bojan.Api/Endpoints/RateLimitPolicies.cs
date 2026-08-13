using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Bojan.Api.Endpoints;

/// <summary>
/// One window, as a permit count and a duration.
/// </summary>
/// <remarks>
/// Bound from configuration so a deployment can widen a limit it has a reason
/// to widen — an office behind one NAT address shares a partition, and eight
/// sign-in attempts per five minutes is eight between all of them. Every value
/// is optional and every default below is the shipped number, so an unset
/// section changes nothing.
/// </remarks>
public sealed class RateLimitWindow
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }

    public RateLimitWindow WithDefaults(int permitLimit, TimeSpan window)
    {
        if (PermitLimit <= 0) PermitLimit = permitLimit;
        if (WindowSeconds <= 0) WindowSeconds = (int)window.TotalSeconds;
        return this;
    }
}

/// <summary>Bound from the <c>RateLimits</c> section; every entry is optional.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    public RateLimitWindow OtpRequest { get; set; } = new();

    public RateLimitWindow Register { get; set; } = new();

    public RateLimitWindow OtpVerify { get; set; } = new();

    public RateLimitWindow AdminLogin { get; set; } = new();

    public RateLimitWindow Coupon { get; set; } = new();

    public RateLimitWindow PlaceOrder { get; set; } = new();

    public RateLimitWindow OrderTracking { get; set; } = new();

    public RateLimitWindow PublicWrite { get; set; } = new();

    public RateLimitWindow AdminWrite { get; set; } = new();

    public RateLimitWindow Upload { get; set; } = new();

    public RateLimitWindow ChatRead { get; set; } = new();

    public RateLimitWindow CatalogueRead { get; set; } = new();

    public RateLimitWindow PaymentCallback { get; set; } = new();

    /// <summary>
    /// The ceiling every request passes under, including the ones no named
    /// policy covers.
    /// </summary>
    /// <remarks>
    /// Raise it for a deployment that genuinely serves that much from one
    /// address — an office behind one NAT, or a mobile carrier's shared egress.
    /// Lowering it below the named policies is possible and would make them
    /// unreachable, which is why the shipped number is well above all of them.
    /// </remarks>
    public RateLimitWindow Global { get; set; } = new();
}

/// <summary>
/// Named rate-limit policies, one per endpoint that needs one.
/// </summary>
/// <remarks>
/// <para>
/// The frontend already rate-limits most of these at its own edge
/// (<c>apps/storefront/src/lib/auth/rate-limit.ts</c>,
/// <c>apps/admin/src/lib/auth/rate-limit.ts</c>) — an in-process limiter that
/// only holds per Next.js instance. This is the second, authoritative layer:
/// per-IP, enforced here regardless of how many frontend instances sit in front
/// of it, and still there when a caller skips the frontend entirely. The
/// windows match the frontend's own numbers so the two layers do not
/// contradict each other.
/// </para>
/// <para>
/// <see cref="OrderTracking"/> is the one with no frontend counterpart, and the
/// tightest. <c>BACKEND.md</c> Phase 4: "Rate-limit it hard and server-side" —
/// it is the only endpoint that will look up an order for an anonymous caller.
/// </para>
/// </remarks>
public static class RateLimitPolicies
{
    public const string OtpRequest = "otp-request";
    public const string Register = "register";
    public const string OtpVerify = "otp-verify";
    public const string AdminLogin = "admin-login";
    public const string Coupon = "coupon";
    public const string PlaceOrder = "place-order";
    public const string OrderTracking = "order-tracking";
    public const string PublicWrite = "public-write";
    public const string AdminWrite = "admin-write";
    public const string Upload = "upload";
    public const string ChatRead = "chat-read";
    public const string CatalogueRead = "catalogue-read";
    public const string PaymentCallback = "payment-callback";

    public static void AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // Bound rather than read here: this runs while services are being
        // registered, and a configuration source added after that point — which
        // is how the test host supplies its own limits — is not in
        // `configuration` yet. Each partition resolves the options per request
        // instead, where the whole configuration is settled.
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            /*
                How long to wait, said out loud.

                A bare 429 tells a caller they have been refused and nothing
                about when to come back, so a client either gives up or retries
                immediately — and retrying immediately against a limiter is how
                a rate limit becomes the load it was meant to prevent. The
                frontend already reads `Retry-After` (see
                `@bojan/config/submit-errors`), which is where its "try again in
                N seconds" sentences come from; it simply never received one
                from this side.
            */
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            // The floor under everything, including whatever is added tomorrow.
            //
            // Named policies cover the endpoints somebody thought about. Every
            // other route had no ceiling at all — the whole of `/me`, the whole
            // of the panel's reads, `/health`, and any route added after this
            // file was last read. That is the wrong default for a public API:
            // "unlimited unless listed" means the list is a thing to remember,
            // and the endpoint nobody remembered is the one that gets found.
            //
            // Deliberately loose. This is not the limit that protects the
            // sign-in form or the coupon check — those are the named policies
            // below, and a global limiter that also runs is a second, stricter
            // gate on top of them, not a replacement. It exists so that one
            // address cannot pin the process by looping any single unlisted
            // endpoint, and it is set well above what a person browsing fast,
            // a panel loading a dashboard, or a crawler behaving itself will
            // ever reach.
            //
            // The shop's own server is exempt for the reason the catalogue
            // policy exempts it: every page rendered server-side arrives from
            // one address, so an address-keyed ceiling there is a ceiling on
            // the whole shop rather than on any shopper.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                PartitionExceptTrustedProxy(limits => limits.Global, 600, TimeSpan.FromMinutes(1)));

            // Mirrors apps/storefront/.../otp/request/route.ts's burst window:
            // 5 requests per minute per client address.
            options.AddPolicy(OtpRequest, PartitionByIp(limits => limits.OtpRequest, 5, TimeSpan.FromMinutes(1)));

            // Registration is the one endpoint whose answer says whether a
            // number is already known to the shop, and it cannot stop saying so
            // without becoming a form that lies about what it did. Closing that
            // properly means verifying the phone before the account exists,
            // which is a change to the sign-up screens rather than to this file.
            // Until then the ceiling is what makes the answer expensive to ask
            // repeatedly: five a minute is a person who mistyped, not someone
            // walking a list.
            options.AddPolicy(Register, PartitionByIp(limits => limits.Register, 5, TimeSpan.FromMinutes(5)));

            // Mirrors the verify route's own limit: 10 attempts per minute.
            options.AddPolicy(OtpVerify, PartitionByIp(limits => limits.OtpVerify, 10, TimeSpan.FromMinutes(1)));

            // Mirrors apps/admin/.../admin-auth/login/route.ts's LOGIN_MAX_ATTEMPTS:
            // 8 attempts per 5 minutes.
            options.AddPolicy(AdminLogin, PartitionByIp(limits => limits.AdminLogin, 8, TimeSpan.FromMinutes(5)));

            // A short coupon code space is walkable from a browser console —
            // the frontend limits this for the same reason.
            options.AddPolicy(Coupon, PartitionByIp(limits => limits.Coupon, 10, TimeSpan.FromMinutes(1)));

            // Mirrors the frontend order route's own 10/minute per customer.
            options.AddPolicy(PlaceOrder, PartitionByIp(limits => limits.PlaceOrder, 10, TimeSpan.FromMinutes(1)));

            // Hard, because this one answers questions about other people's
            // orders when given the right pair.
            options.AddPolicy(OrderTracking, PartitionByIp(limits => limits.OrderTracking, 10, TimeSpan.FromMinutes(5)));

            // The frontend allows 3-5 per minute on these forms; 5 covers the
            // most generous of them.
            options.AddPolicy(PublicWrite, PartitionByIp(limits => limits.PublicWrite, 5, TimeSpan.FromMinutes(1)));

            // Mirrors the panel's own admin-write limit: 60 per minute.
            options.AddPolicy(AdminWrite, PartitionByIp(limits => limits.AdminWrite, 60, TimeSpan.FromMinutes(1)));

            // Uploads are heavier than a JSON write, so they get their own,
            // lower ceiling rather than sharing one.
            options.AddPolicy(Upload, PartitionByIp(limits => limits.Upload, 20, TimeSpan.FromMinutes(1)));

            // The chat widget polls every four seconds while it is open, so
            // this has to clear roughly fifteen a minute for one honest tab.
            // The ceiling is what stops the read being a free oracle: the
            // visitor id is the only thing naming a conversation, and without
            // a limit ids can be walked as fast as the server answers.
            options.AddPolicy(ChatRead, PartitionByIp(limits => limits.ChatRead, 120, TimeSpan.FromMinutes(1)));

            // The public catalogue, for callers reaching it directly.
            //
            // Generous, because this is what an honest shopper browsing quickly
            // looks like and what a search engine crawling politely looks like —
            // the ceiling is here to stop the reads being free to walk, not to
            // pace anybody.
            //
            // The shop's own server is exempt, and has to be. Every page the
            // storefront renders reads the catalogue server-side, and those
            // calls arrive from one address — the Next.js host — so a per-IP
            // ceiling on them is not a limit per shopper, it is a limit on the
            // whole shop's rendering, shared by everyone at once. Applied
            // without that exemption this took the site down: the layout alone
            // reads the shipping methods on every page, so once the shared
            // window filled, the sign-in and basket screens started rendering
            // the error boundary.
            options.AddPolicy(
                CatalogueRead,
                PartitionExceptTrustedProxy(limits => limits.CatalogueRead, 300, TimeSpan.FromMinutes(1)));

            // Every call here is a round trip to the payment provider on this
            // shop's account, so the ceiling is a spend limit as much as an
            // abuse limit. Twenty a minute clears a shopper who refreshes the
            // callback page a few times and one who genuinely pays twice; it
            // does not clear a loop.
            options.AddPolicy(
                PaymentCallback,
                PartitionByIp(limits => limits.PaymentCallback, 20, TimeSpan.FromMinutes(1)));
        });
    }

    /// <summary>
    /// The same partition as <see cref="PartitionByIp"/>, except that the shop's
    /// own server is not limited.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the storefront and the panel hold <c>X-Api-Key</c>; it is server-side
    /// configuration and never reaches a browser, which is the whole basis of
    /// the trusted-proxy scheme. So a request carrying it is this shop rendering
    /// a page for somebody, and a request without it is a caller talking to the
    /// API directly — which is who a public read limit is for.
    /// </para>
    /// <para>
    /// Limiting the first group by address means limiting every shopper as one
    /// client, because server-side rendering gives them all the same source
    /// address. The frontend already applies its own per-shopper ceiling at its
    /// edge, keyed on the forwarded client address, so that group is not
    /// unlimited — it is limited in the one place that can tell shoppers apart.
    /// </para>
    /// <para>
    /// Deliberately not applied to the other policies. Those guard actions —
    /// signing in, guessing a coupon, placing an order — where the frontend's
    /// edge limit is the per-shopper one and this is the floor underneath it.
    /// They share the same blind spot about server-side calls, and closing it
    /// properly means forwarding the shopper's address from the proxies rather
    /// than exempting them here.
    /// </para>
    /// </remarks>
    private static Func<HttpContext, RateLimitPartition<string>> PartitionExceptTrustedProxy(
        Func<RateLimitOptions, RateLimitWindow> select,
        int permitLimit,
        TimeSpan window)
    {
        var limited = PartitionByIp(select, permitLimit, window);

        return httpContext =>
        {
            // Through the monitor and by scheme name, not IOptions<T>.
            // TrustedProxyOptions is an authentication scheme's options type, so
            // it is configured per scheme — the plain IOptions<T> resolves a
            // default-constructed instance whose key is null, which silently
            // meant nothing was ever exempt.
            var configured = httpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<Auth.TrustedProxyOptions>>()
                .Get(Auth.TrustedProxyOptions.SchemeName)
                .ApiKey;

            var presented = httpContext.Request.Headers[Auth.TrustedProxyOptions.ApiKeyHeader].FirstOrDefault();

            if (!string.IsNullOrEmpty(configured)
                && !string.IsNullOrEmpty(presented)
                && CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(presented),
                    Encoding.UTF8.GetBytes(configured)))
            {
                return RateLimitPartition.GetNoLimiter("trusted-proxy");
            }

            return limited(httpContext);
        };
    }

    /// <summary>
    /// Buckets by the connecting address, never by a header.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to read the left-most entry of <c>X-Forwarded-For</c>, which is
    /// the one place in that header nobody trustworthy writes: proxies append,
    /// so the left-most value is whatever the caller sent. A different one per
    /// request bought a fresh window every time and made every limit above a
    /// formality.
    /// </para>
    /// <para>
    /// <c>RemoteIpAddress</c> is used instead, which
    /// <see cref="Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersMiddleware"/>
    /// has already rewritten from the trusted end of that chain — see the
    /// forwarded-headers configuration in <c>Program.cs</c>. Reading the header
    /// here as well would be reintroducing the thing that middleware exists to
    /// get right.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The caller's address, as one partition key per caller.
    /// </summary>
    /// <remarks>
    /// An IPv4 client reaching a dual-stack socket arrives as an IPv4-mapped
    /// IPv6 address — <c>::ffff:172.20.0.1</c> — and the same client over a
    /// plain IPv4 socket arrives as <c>172.20.0.1</c>. Left as they are, those
    /// are two keys, two windows and twice the limit for one person. Both
    /// spellings appear in this deployment's own request log.
    /// </remarks>
    private static string PartitionAddress(HttpContext httpContext)
    {
        if (httpContext.Connection.RemoteIpAddress is not { } address)
        {
            return "unknown";
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    private static Func<HttpContext, RateLimitPartition<string>> PartitionByIp(
        Func<RateLimitOptions, RateLimitWindow> select,
        int permitLimit,
        TimeSpan window) =>
        httpContext =>
        {
            var configured = select(
                httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value)
                .WithDefaults(permitLimit, window);

            var address = PartitionAddress(httpContext);

            // The key carries the window, so a limit changed in configuration
            // opens a fresh partition rather than reusing one sized by the old
            // numbers.
            return RateLimitPartition.GetFixedWindowLimiter(
                $"{address}|{configured.PermitLimit}|{configured.WindowSeconds}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configured.PermitLimit,
                    Window = TimeSpan.FromSeconds(configured.WindowSeconds),
                    QueueLimit = 0,
                });
        };
}
