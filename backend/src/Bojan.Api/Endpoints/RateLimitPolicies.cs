using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Bojan.Api.Endpoints;

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
    public const string OtpVerify = "otp-verify";
    public const string AdminLogin = "admin-login";
    public const string Coupon = "coupon";
    public const string PlaceOrder = "place-order";
    public const string OrderTracking = "order-tracking";
    public const string PublicWrite = "public-write";
    public const string AdminWrite = "admin-write";
    public const string Upload = "upload";

    public static void AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Mirrors apps/storefront/.../otp/request/route.ts's burst window:
            // 5 requests per minute per client address.
            options.AddPolicy(OtpRequest, PartitionByIp(permitLimit: 5, window: TimeSpan.FromMinutes(1)));

            // Mirrors the verify route's own limit: 10 attempts per minute.
            options.AddPolicy(OtpVerify, PartitionByIp(permitLimit: 10, window: TimeSpan.FromMinutes(1)));

            // Mirrors apps/admin/.../admin-auth/login/route.ts's LOGIN_MAX_ATTEMPTS:
            // 8 attempts per 5 minutes.
            options.AddPolicy(AdminLogin, PartitionByIp(permitLimit: 8, window: TimeSpan.FromMinutes(5)));

            // A short coupon code space is walkable from a browser console —
            // the frontend limits this for the same reason.
            options.AddPolicy(Coupon, PartitionByIp(permitLimit: 10, window: TimeSpan.FromMinutes(1)));

            // Mirrors the frontend order route's own 10/minute per customer.
            options.AddPolicy(PlaceOrder, PartitionByIp(permitLimit: 10, window: TimeSpan.FromMinutes(1)));

            // Hard, because this one answers questions about other people's
            // orders when given the right pair.
            options.AddPolicy(OrderTracking, PartitionByIp(permitLimit: 10, window: TimeSpan.FromMinutes(5)));

            // The frontend allows 3-5 per minute on these forms; 5 covers the
            // most generous of them.
            options.AddPolicy(PublicWrite, PartitionByIp(permitLimit: 5, window: TimeSpan.FromMinutes(1)));

            // Mirrors the panel's own admin-write limit: 60 per minute.
            options.AddPolicy(AdminWrite, PartitionByIp(permitLimit: 60, window: TimeSpan.FromMinutes(1)));

            // Uploads are heavier than a JSON write, so they get their own,
            // lower ceiling rather than sharing one.
            options.AddPolicy(Upload, PartitionByIp(permitLimit: 20, window: TimeSpan.FromMinutes(1)));
        });
    }

    private static Func<HttpContext, RateLimitPartition<string>> PartitionByIp(int permitLimit, TimeSpan window) =>
        httpContext =>
        {
            // X-Forwarded-For is only trustworthy behind a proxy that
            // overwrites it — the same caveat the frontend's own
            // clientKey() carries, and the same deployment assumption.
            var address = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
            });
        };
}
