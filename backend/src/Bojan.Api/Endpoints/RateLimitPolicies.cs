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
/// sign-in attempts per five minutes is eight for the whole office. The
/// defaults are the shipped numbers, so an unset section changes nothing.
/// </remarks>
public sealed class RateLimitWindow
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }

    public RateLimitWindow WithDefaults(int permitLimit, int windowSeconds)
    {
        if (PermitLimit <= 0) PermitLimit = permitLimit;
        if (WindowSeconds <= 0) WindowSeconds = windowSeconds;
        return this;
    }
}

/// <summary>Bound from the <c>RateLimits</c> section; every entry is optional.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    public RateLimitWindow OtpRequest { get; set; } = new();

    public RateLimitWindow OtpVerify { get; set; } = new();

    public RateLimitWindow AdminLogin { get; set; } = new();

    public RateLimitWindow Coupon { get; set; } = new();

    public RateLimitWindow PlaceOrder { get; set; } = new();

    public RateLimitWindow OrderTracking { get; set; } = new();

    public RateLimitWindow PublicWrite { get; set; } = new();

    public RateLimitWindow AdminWrite { get; set; } = new();

    public RateLimitWindow Upload { get; set; } = new();
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
/// default windows match the frontend's own numbers so the two layers do not
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
    public const string OtpVerify = "otp-verify";
    public const string AdminLogin = "admin-login";
    public const string Coupon = "coupon";
    public const string PlaceOrder = "place-order";
    public const string OrderTracking = "order-tracking";
    public const string PublicWrite = "public-write";
    public const string AdminWrite = "admin-write";
    public const string Upload = "upload";

    public static void AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // Bound rather than read here: this method runs while services are
        // being registered, and configuration sources added after that point —
        // which is how the test host supplies its own limits — are not in
        // `configuration` yet. Each partition resolves the options per request
        // instead, where the whole configuration is settled.
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Mirrors apps/storefront/.../otp/request/route.ts's burst window:
            // 5 requests per minute per client address.
            options.AddPolicy(OtpRequest, PartitionByIp(limits => limits.OtpRequest, 5, 60));

            // Mirrors the verify route's own limit: 10 attempts per minute.
            options.AddPolicy(OtpVerify, PartitionByIp(limits => limits.OtpVerify, 10, 60));

            // Mirrors apps/admin/.../admin-auth/login/route.ts's LOGIN_MAX_ATTEMPTS:
            // 8 attempts per 5 minutes. The second factor's exchange shares this
            // policy — it is the other half of the same attempt.
            options.AddPolicy(AdminLogin, PartitionByIp(limits => limits.AdminLogin, 8, 300));

            // A short coupon code space is walkable from a browser console —
            // the frontend limits this for the same reason.
            options.AddPolicy(Coupon, PartitionByIp(limits => limits.Coupon, 10, 60));

            // Mirrors the frontend order route's own 10/minute per customer.
            options.AddPolicy(PlaceOrder, PartitionByIp(limits => limits.PlaceOrder, 10, 60));

            // Hard, because this one answers questions about other people's
            // orders when given the right pair.
            options.AddPolicy(OrderTracking, PartitionByIp(limits => limits.OrderTracking, 10, 300));

            // The frontend allows 3-5 per minute on these forms; 5 covers the
            // most generous of them.
            options.AddPolicy(PublicWrite, PartitionByIp(limits => limits.PublicWrite, 5, 60));

            // Mirrors the panel's own admin-write limit: 60 per minute.
            options.AddPolicy(AdminWrite, PartitionByIp(limits => limits.AdminWrite, 60, 60));

            // Uploads are heavier than a JSON write, so they get their own,
            // lower ceiling rather than sharing one.
            options.AddPolicy(Upload, PartitionByIp(limits => limits.Upload, 20, 60));
        });
    }

    private static Func<HttpContext, RateLimitPartition<string>> PartitionByIp(
        Func<RateLimitOptions, RateLimitWindow> select,
        int permitLimit,
        int windowSeconds) =>
        httpContext =>
        {
            var window = select(
                httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value)
                .WithDefaults(permitLimit, windowSeconds);

            // ForwardedHeadersMiddleware (Program.cs) already rewrote this to the
            // real client address when the request came through a proxy listed in
            // Api:TrustedProxies — reading X-Forwarded-For directly here would let
            // a client pick a fresh partition on every request by spoofing it.
            var address = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // The key carries the window, so changing a limit at runtime opens a
            // new partition rather than reusing one sized by the old numbers.
            return RateLimitPartition.GetFixedWindowLimiter(
                $"{address}|{window.PermitLimit}|{window.WindowSeconds}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = window.PermitLimit,
                    Window = TimeSpan.FromSeconds(window.WindowSeconds),
                    QueueLimit = 0,
                });
        };
}
