import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { getSession } from '@/lib/auth/server';
import { ApiError, api, useMockData } from '@/lib/api/client';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

/**
 * Screen 16 — confirms whichever pending phone challenge is active for the
 * signed-in customer: verifying the current phone, or completing a phone
 * change — same endpoint on the backend for both, see
 * `AccountVerificationEndpoints.ConfirmPhoneVerification`.
 */
export async function POST(request: Request) {
  const session = await getSession();
  if (!session) {
    return NextResponse.json({ error: 'برای این کار وارد حساب خود شوید.' }, { status: 401 });
  }

  const limit = rateLimit(clientKey(request, `phone-verify-confirm:${session.sub}`), 10, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'تلاش‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const body = (await request.json().catch(() => null)) as { code?: unknown } | null;
  const code = normalizeDigitsInput(String(body?.code ?? ''));

  if (!/^\d{4,6}$/.test(code)) {
    return NextResponse.json({ error: 'کد تایید را کامل وارد کنید.' }, { status: 400 });
  }

  if (useMockData) {
    return NextResponse.json({ ok: true });
  }

  try {
    await api.post(
      '/account/phone/verify/confirm',
      { code },
      {
        headers: {
          'X-Customer-Id': session.sub,
          ...(session.stamp ? { 'X-Customer-Stamp': session.stamp } : null),
          ...(session.token ? { Authorization: `Bearer ${session.token}` } : null),
        },
      },
    );
  } catch (cause) {
    const title =
      cause instanceof ApiError && typeof cause.body === 'object' && cause.body !== null
        ? (cause.body as { title?: unknown }).title
        : null;

    if (title === 'phone-verification-attempts-exhausted') {
      return NextResponse.json(
        { error: 'تعداد تلاش‌ها بیش از حد مجاز است. کد تازه‌ای درخواست کنید.' },
        { status: 429 },
      );
    }

    if (title === 'phone-verification-expired') {
      return NextResponse.json(
        { error: 'کد تایید منقضی شده است. دوباره درخواست دهید.' },
        { status: 400 },
      );
    }

    if (title === 'phone-taken') {
      return NextResponse.json(
        { error: 'این شماره قبلاً ثبت شده است.' },
        { status: 409 },
      );
    }

    if (title === 'phone-verification-incorrect') {
      return NextResponse.json({ error: 'کد تایید نادرست است.' }, { status: 400 });
    }

    return NextResponse.json(
      { error: 'تایید کد ممکن نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }

  return NextResponse.json({ ok: true });
}
