import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';
import { api, useMockData } from '@/lib/api/client';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import {
  VISITOR_COOKIE,
  VISITOR_MAX_AGE,
  signVisitorId,
  verifyVisitorId,
  visitorCookieOptions,
} from '@/lib/chat/visitor';

/**
 * The chat widget's write side — see `../route.ts` for why there is no session
 * check.
 *
 * This is where a visitor is minted: the first message creates the id and sets
 * the cookie, so an id only ever exists because someone actually said
 * something. Nothing in the browser chooses it.
 */
export async function POST(request: Request) {
  const store = await cookies();
  const existing = await verifyVisitorId(store.get(VISITOR_COOKIE)?.value);

  // Keyed on the address rather than the visitor id: the id is now ours to
  // issue, so keying on it would have let a caller reset their own window by
  // dropping the cookie.
  const limit = rateLimit(clientKey(request, 'chat-write'), 20, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'پیام‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const body = (await request.json().catch(() => null)) as { body?: unknown } | null;
  const text = typeof body?.body === 'string' ? body.body.trim() : '';

  if (!text || text.length > 4000) {
    return NextResponse.json({ error: 'متن پیام نامعتبر است.' }, { status: 400 });
  }

  const visitorId = existing ?? crypto.randomUUID();

  if (!useMockData) {
    try {
      await api.post(`/chat/${visitorId}/messages`, { body: text });
    } catch {
      return NextResponse.json(
        { error: 'ارسال پیام ممکن نشد. کمی بعد دوباره تلاش کنید.' },
        { status: 502 },
      );
    }
  }

  const response = NextResponse.json({ ok: true, ...(useMockData ? { mock: true } : null) });

  // Written on every accepted message, not only the first: it slides the
  // month forward for someone still talking to support.
  response.cookies.set(VISITOR_COOKIE, await signVisitorId(visitorId), {
    ...visitorCookieOptions,
    maxAge: VISITOR_MAX_AGE,
  });

  return response;
}
