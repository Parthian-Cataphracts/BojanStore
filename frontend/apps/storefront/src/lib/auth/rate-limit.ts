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

import { clientAddress } from '@bojan/config/client-address';

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
 * the start — see `@bojan/config/client-address` for which end and why.
 *
 * With no header at all the bucket is shared rather than absent: degrading to
 * one limit for everyone is a worse service than usual, but it is still a limit.
 */
export function clientKey(request: Request, scope: string): string {
  return `${scope}:${clientAddress(request.headers) ?? 'unknown'}`;
}
