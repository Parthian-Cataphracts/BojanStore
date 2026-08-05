import { NextResponse } from 'next/server';
import { getSession } from '@/lib/auth/server';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import { api, useMockData } from '@/lib/api/client';

/**
 * Unread notification count for the header bell.
 *
 * A GET of its own rather than an entry in the `[action]` proxy, which
 * forwards writes. A literal segment takes routing precedence over `[action]`,
 * so this handler is the one that runs.
 *
 * The bell is on every page, so this is polled far more often than anything
 * else here — hence a count from the API rather than the feed with the read
 * ones filtered out at this layer.
 */
export const dynamic = 'force-dynamic';

export async function GET(request: Request) {
  const session = await getSession();

  // Zero rather than 401 for a guest. The bell renders for everyone — it is
  // part of the header — and a signed-out visitor loading any page would
  // otherwise put an authentication error in their console on every
  // navigation. There is nothing withheld here: a caller with no session has
  // no notifications, and that is exactly what this says.
  if (!session) {
    return NextResponse.json({ count: 0 });
  }

  // Bucketed on the session rather than the address: the bell fires once per
  // navigation, and a household or an office behind one address would otherwise
  // spend a shared window on each other's page views.
  const limit = rateLimit(`unread-count:${session.sub}:${clientKey(request, 'unread-count')}`, 120, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  if (useMockData) {
    return NextResponse.json({ count: 0 });
  }

  try {
    const result = await api.get<{ count: number }>('/me/notifications/unread-count', {
      cache: 'no-store',
      auth: true,
    });
    return NextResponse.json({ count: result.count });
  } catch {
    // A badge is not worth surfacing an error for. Showing none is the same
    // thing the page showed a moment ago.
    return NextResponse.json({ count: 0 });
  }
}
