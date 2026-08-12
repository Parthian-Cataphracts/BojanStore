import { NextResponse } from 'next/server';
import { getAdminSession } from '@/lib/auth/server';
import { rateLimit } from '@/lib/auth/rate-limit';
import { useMockData } from '@/lib/api/mock-data';

/**
 * The one write the generic `[resource]` proxy can't express: the backend
 * route takes the visitor id in the path, not the body, so this is a small
 * bespoke proxy rather than an entry in `lib/api/resources` — the same
 * reason the upload route isn't one either.
 */
export async function POST(request: Request) {
  const session = await getAdminSession();
  if (!session || !['owner', 'support'].includes(session.role)) {
    return NextResponse.json({ error: 'دسترسی ندارید.' }, { status: 401 });
  }

  const limit = rateLimit(`admin-chat-reply:${session.sub}`, 60, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const payload = (await request.json().catch(() => null)) as
    | { visitorId?: unknown; body?: unknown }
    | null;
  const visitorId = typeof payload?.visitorId === 'string' ? payload.visitorId : '';
  const body = typeof payload?.body === 'string' ? payload.body.trim() : '';

  if (!visitorId || !body || body.length > 4000) {
    return NextResponse.json({ error: 'داده ارسالی معتبر نیست.' }, { status: 400 });
  }

  if (useMockData) {
    return NextResponse.json({ ok: true, mock: true });
  }

  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  try {
    // No `/admin` segment here. `API_BASE_URL` already ends in one — see the
    // panel's own `.env.example` and the compose file — so writing it again
    // asked the API for `/api/admin/admin/chat/...`, which is a 404 dressed up
    // as "ارسال پاسخ انجام نشد." on the operator's screen. It never showed up
    // in development because the mock branch above returns before this line.
    const upstream = await fetch(
      `${base.replace(/\/$/, '')}/chat/conversations/${visitorId}/reply`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json',
          'X-Admin-User': session.sub,
          'X-Admin-Stamp': session.stamp,
          ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
        },
        body: JSON.stringify({ body }),
        cache: 'no-store',
      },
    );

    if (!upstream.ok) {
      return NextResponse.json({ error: 'ارسال پاسخ انجام نشد.' }, { status: upstream.status });
    }

    return NextResponse.json((await upstream.json().catch(() => ({ ok: true }))) as unknown);
  } catch {
    return NextResponse.json(
      { error: 'ارتباط با سرور برقرار نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }
}
