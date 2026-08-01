/**
 * Fixed-window rate limiter for the auth and lookup endpoints.
 *
 * Deliberately in-process: it needs no infrastructure and it stops the obvious
 * abuse — hammering the OTP endpoint to enumerate numbers, or brute-forcing an
 * order-tracking lookup. Behind more than one instance each replica keeps its
 * own window, so the effective limit multiplies by the replica count; once the
 * .NET backend is in front of these routes, enforcement moves there and this
 * becomes the second line rather than the only one.
 */

interface Window {
  count: number;
  resetAt: number;
}

const windows = new Map<string, Window>();

/** Drop expired windows so a long-lived process does not grow unbounded. */
function sweep(now: number): void {
  if (windows.size < 1000) return;
  for (const [key, window] of windows) {
    if (window.resetAt <= now) windows.delete(key);
  }
}

export interface RateLimitResult {
  allowed: boolean;
  /** Seconds until the window resets — surfaced as `Retry-After`. */
  retryAfter: number;
  remaining: number;
}

export function rateLimit(key: string, limit: number, windowSeconds: number): RateLimitResult {
  const now = Date.now();
  sweep(now);

  const existing = windows.get(key);

  if (!existing || existing.resetAt <= now) {
    windows.set(key, { count: 1, resetAt: now + windowSeconds * 1000 });
    return { allowed: true, retryAfter: 0, remaining: limit - 1 };
  }

  existing.count += 1;
  const retryAfter = Math.max(1, Math.ceil((existing.resetAt - now) / 1000));

  if (existing.count > limit) {
    return { allowed: false, retryAfter, remaining: 0 };
  }

  return { allowed: true, retryAfter, remaining: limit - existing.count };
}

/**
 * Best-effort client address.
 *
 * `x-forwarded-for` is only trustworthy behind a proxy that overwrites it —
 * which is how this app is meant to be deployed. The fallback bucket means a
 * missing header degrades to a shared limit rather than to no limit at all.
 */
export function clientKey(request: Request, scope: string): string {
  const forwarded = request.headers.get('x-forwarded-for');
  const address = forwarded?.split(',')[0]?.trim() || request.headers.get('x-real-ip') || 'unknown';
  return `${scope}:${address}`;
}
