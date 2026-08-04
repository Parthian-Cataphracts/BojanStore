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
 * The client address, taken from the end of the forwarding chain rather than
 * the start.
 *
 * `X-Forwarded-For` is a list, and every proxy *appends* to it — nginx's
 * `$proxy_add_x_forwarded_for` included. So the left-most entry is not the
 * client's address, it is whatever the client sent, and reading it made every
 * limit here a formality: a different value per request bought a fresh window
 * each time, and with it unlimited sign-in codes, coupon guesses and tracking
 * lookups.
 *
 * The trustworthy entry is the one your own proxy wrote, counting back from the
 * right by however many proxies stand in front of this app. One by default,
 * which is the topology the compose file describes — everything published on
 * loopback with a single reverse proxy in front. Set `TRUSTED_PROXY_HOPS` if
 * there are more (a CDN in front of nginx is two).
 *
 * With no header at all the bucket is shared rather than absent: degrading to
 * one limit for everyone is a worse service than usual, but it is still a limit.
 */
const TRUSTED_PROXY_HOPS = Math.max(1, Number(process.env.TRUSTED_PROXY_HOPS ?? 1) || 1);

export function clientKey(request: Request, scope: string): string {
  const chain = (request.headers.get('x-forwarded-for') ?? '')
    .split(',')
    .map((entry) => entry.trim())
    .filter(Boolean);

  // Counting from the right: the last entry was written by the nearest proxy,
  // the one before it by the proxy in front of that, and so on. Anything the
  // client supplied sits to the left of all of them and is never chosen.
  const trusted = chain.length >= TRUSTED_PROXY_HOPS
    ? chain[chain.length - TRUSTED_PROXY_HOPS]
    : undefined;

  const address = trusted || request.headers.get('x-real-ip')?.trim() || 'unknown';
  return `${scope}:${address}`;
}
