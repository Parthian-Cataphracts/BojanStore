import { NextResponse } from 'next/server';
import { getSession } from '@/lib/auth/server';
import { ApiError, api, useMockData } from '@/lib/api/client';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

/** Screen 16 — send a code to the account's own current phone. */
export async function POST(request: Request) {
  const session = await getSession();
  if (!session) {
    return NextResponse.json({ error: 'برای این کار وارد حساب خود شوید.' }, { status: 401 });
  }

  const limit = rateLimit(clientKey(request, `phone-verify-request:${session.sub}`), 5, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  if (useMockData) {
    return NextResponse.json({ ok: true });
  }

  try {
    await api.post(
      '/account/phone/verify/request',
      undefined,
      {
        headers: {
          'X-Customer-Id': session.sub,
          ...(session.stamp ? { 'X-Customer-Stamp': session.stamp } : null),
          ...(session.token ? { Authorization: `Bearer ${session.token}` } : null),
        },
      },
    );
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 429) {
      const retryAfterSeconds =
        typeof cause.body === 'object' &&
        cause.body !== null &&
        typeof (cause.body as { retryAfterSeconds?: unknown }).retryAfterSeconds === 'number'
          ? (cause.body as { retryAfterSeconds: number }).retryAfterSeconds
          : 120;

      return NextResponse.json(
        { error: 'کد قبلی هنوز معتبر است.', retryAfterSeconds },
        { status: 429, headers: { 'Retry-After': String(retryAfterSeconds) } },
      );
    }

    return NextResponse.json(
      { error: 'ارسال کد تایید ممکن نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }

  return NextResponse.json({ ok: true });
}
