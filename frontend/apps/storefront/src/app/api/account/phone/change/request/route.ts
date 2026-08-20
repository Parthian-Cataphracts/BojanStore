import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { getSession } from '@/lib/auth/server';
import { ApiError, api, useMockData } from '@/lib/api/client';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

/**
 * Screen 16 — start changing the account's phone: sends a code to the
 * candidate number, which does not take effect until `../../verify/confirm`
 * accepts that code.
 */
export async function POST(request: Request) {
  const session = await getSession();
  if (!session) {
    return NextResponse.json({ error: 'برای این کار وارد حساب خود شوید.' }, { status: 401 });
  }

  const limit = rateLimit(clientKey(request, `phone-change-request:${session.sub}`), 5, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const body = (await request.json().catch(() => null)) as { newPhone?: unknown } | null;
  const newPhone = normalizeDigitsInput(String(body?.newPhone ?? ''));

  if (!/^09\d{9}$/.test(newPhone)) {
    return NextResponse.json(
      { error: 'شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.' },
      { status: 400 },
    );
  }

  if (useMockData) {
    return NextResponse.json({ ok: true });
  }

  try {
    await api.post(
      '/account/phone/change/request',
      { newPhone },
      {
        headers: {
          'X-Customer-Id': session.sub,
          ...(session.stamp ? { 'X-Customer-Stamp': session.stamp } : null),
          ...(session.token ? { Authorization: `Bearer ${session.token}` } : null),
        },
      },
    );
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 409) {
      return NextResponse.json(
        { error: 'این شماره قبلاً ثبت شده است.' },
        { status: 409 },
      );
    }

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
