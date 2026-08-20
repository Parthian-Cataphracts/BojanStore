import { NextResponse } from 'next/server';
import { getSession } from '@/lib/auth/server';
import { ApiError, api, useMockData } from '@/lib/api/client';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

/**
 * Screen 16 — request the email verification link.
 *
 * Mirrors `../../otp/request`'s cooldown handling: the backend's 429 carries
 * `retryAfterSeconds`, which the profile form uses to start its countdown at
 * the real remaining time rather than guessing.
 */

const NO_EMAIL_KEY = 'no-email';

export async function POST(request: Request) {
  const session = await getSession();
  if (!session) {
    return NextResponse.json({ error: 'برای این کار وارد حساب خود شوید.' }, { status: 401 });
  }

  const limit = rateLimit(clientKey(request, `email-verify-request:${session.sub}`), 5, 60);
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
      '/account/email/verify/request',
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
    if (cause instanceof ApiError && cause.status === 400) {
      const title = typeof cause.body === 'object' && cause.body !== null
        ? (cause.body as { title?: unknown }).title
        : null;

      if (title === NO_EMAIL_KEY) {
        return NextResponse.json(
          { error: 'برای دریافت لینک تایید، ابتدا یک ایمیل ثبت کنید.' },
          { status: 400 },
        );
      }
    }

    if (cause instanceof ApiError && cause.status === 429) {
      const retryAfterSeconds =
        typeof cause.body === 'object' &&
        cause.body !== null &&
        typeof (cause.body as { retryAfterSeconds?: unknown }).retryAfterSeconds === 'number'
          ? (cause.body as { retryAfterSeconds: number }).retryAfterSeconds
          : 120;

      return NextResponse.json(
        { error: 'لینک تایید قبلی هنوز معتبر است.', retryAfterSeconds },
        { status: 429, headers: { 'Retry-After': String(retryAfterSeconds) } },
      );
    }

    return NextResponse.json(
      { error: 'ارسال لینک تایید ممکن نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }

  return NextResponse.json({ ok: true });
}
