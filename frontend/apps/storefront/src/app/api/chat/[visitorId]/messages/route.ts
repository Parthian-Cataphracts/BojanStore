import { NextResponse } from 'next/server';
import { api, useMockData } from '@/lib/api/client';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

/** The chat widget's write side — see `../route.ts` for why there is no session check. */
export async function POST(
  request: Request,
  { params }: { params: Promise<{ visitorId: string }> },
) {
  const { visitorId } = await params;

  const limit = rateLimit(clientKey(request, `chat:${visitorId}`), 20, 60);
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

  if (useMockData) return NextResponse.json({ ok: true, mock: true });

  try {
    await api.post(`/chat/${visitorId}/messages`, { body: text });
    return NextResponse.json({ ok: true });
  } catch {
    return NextResponse.json(
      { error: 'ارسال پیام ممکن نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }
}
