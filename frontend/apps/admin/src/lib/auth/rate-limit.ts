/**
 * Fixed-window rate limiter for the panel's sign-in endpoint.
 *
 * In-process, so behind more than one instance each replica keeps its own
 * window. That is enough to blunt a password-guessing run against the panel and
 * needs no infrastructure; once the .NET backend authenticates admins, the
 * authoritative lockout lives there and this becomes the outer guard.
 */

interface Window {
  count: number;
  resetAt: number;
}

const windows = new Map<string, Window>();

function sweep(now: number): void {
  if (windows.size < 500) return;
  for (const [key, window] of windows) {
    if (window.resetAt <= now) windows.delete(key);
  }
}

export interface RateLimitResult {
  allowed: boolean;
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

  return existing.count > limit
    ? { allowed: false, retryAfter, remaining: 0 }
    : { allowed: true, retryAfter, remaining: limit - existing.count };
}

export function clientKey(request: Request, scope: string): string {
  const forwarded = request.headers.get('x-forwarded-for');
  const address = forwarded?.split(',')[0]?.trim() || request.headers.get('x-real-ip') || 'unknown';
  return `${scope}:${address}`;
}
