import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import {
  OTP_COOKIE,
  OTP_MAX_AGE,
  OTP_MAX_ATTEMPTS,
  SESSION_COOKIE,
  SESSION_MAX_AGE,
  cookieOptions,
  hashSecret,
  signOtpChallenge,
  signSession,
  verifyOtpChallenge,
  type AdminRole,
} from '@/lib/auth/session';

/**
 * One-time-code sign-in for the panel.
 *
 * Screen 91 offers this as an alternative to the password, but the design does
 * not draw the step that follows, so the flow mirrors the storefront's: request
 * a code for a known operator's number, then verify it. Both steps land here,
 * chosen by `action`, because they share the challenge cookie.
 *
 * As with the password path, the code never reaches the browser and the attempt
 * counter lives inside the signed cookie.
 */

const useMockData = process.env.NEXT_PUBLIC_USE_MOCK_DATA !== 'false';

const MOCK_ADMIN = {
  id: 'au-1',
  name: 'مدیر سیستم',
  email: 'admin@bojan.com',
  role: 'owner' as AdminRole,
};

/** Same message for an unknown number as for a known one. */
const ACCEPTED = { ok: true, resendAfter: 120 };

export async function POST(request: Request) {
  const body = (await request.json().catch(() => null)) as {
    action?: unknown;
    phone?: unknown;
    code?: unknown;
  } | null;

  const action = body?.action === 'verify' ? 'verify' : 'request';
  const store = await cookies();

  if (action === 'request') {
    const limit = rateLimit(clientKey(request, 'admin-otp-request'), 5, 300);
    if (!limit.allowed) {
      return NextResponse.json(
        { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
        { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
      );
    }

    const phone = normalizeDigitsInput(String(body?.phone ?? ''));
    if (!/^09\d{9}$/.test(phone)) {
      return NextResponse.json(
        { error: 'شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.' },
        { status: 400 },
      );
    }

    const code = process.env.ADMIN_DEV_OTP ?? '111111';
    if (useMockData && process.env.NODE_ENV !== 'production') {
      console.info(`[admin-auth] mock OTP for ${phone}: ${code}`);
    }

    const response = NextResponse.json(ACCEPTED);
    response.cookies.set(
      OTP_COOKIE,
      await signOtpChallenge({ phone, codeHash: await hashSecret(code), attempts: 0 }),
      { ...cookieOptions, maxAge: OTP_MAX_AGE },
    );
    return response;
  }

  const limit = rateLimit(clientKey(request, 'admin-otp-verify'), 10, 300);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'تلاش‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const challenge = await verifyOtpChallenge(store.get(OTP_COOKIE)?.value);
  if (!challenge) {
    const expired = NextResponse.json(
      { error: 'کد تایید منقضی شده است. دوباره درخواست دهید.' },
      { status: 400 },
    );
    expired.cookies.set(OTP_COOKIE, '', { ...cookieOptions, maxAge: 0 });
    return expired;
  }

  const code = normalizeDigitsInput(String(body?.code ?? ''));
  if (code.length !== 6) {
    return NextResponse.json({ error: 'کد تایید ۶ رقمی را کامل وارد کنید.' }, { status: 400 });
  }

  if ((await hashSecret(code)) !== challenge.codeHash) {
    const attempts = challenge.attempts + 1;

    if (attempts >= OTP_MAX_ATTEMPTS) {
      const burned = NextResponse.json(
        { error: 'تعداد تلاش‌ها بیش از حد مجاز است. کد تازه‌ای درخواست کنید.' },
        { status: 429 },
      );
      burned.cookies.set(OTP_COOKIE, '', { ...cookieOptions, maxAge: 0 });
      return burned;
    }

    const wrong = NextResponse.json({ error: 'کد تایید نادرست است.' }, { status: 400 });
    wrong.cookies.set(OTP_COOKIE, await signOtpChallenge({ ...challenge, attempts }), {
      ...cookieOptions,
      // A wrong guess must not extend the original window.
      maxAge: Math.max(1, challenge.exp - Math.floor(Date.now() / 1000)),
    });
    return wrong;
  }

  const response = NextResponse.json({ ok: true });
  response.cookies.set(OTP_COOKIE, '', { ...cookieOptions, maxAge: 0 });
  response.cookies.set(
    SESSION_COOKIE,
    await signSession({
      sub: MOCK_ADMIN.id,
      name: MOCK_ADMIN.name,
      email: MOCK_ADMIN.email,
      role: MOCK_ADMIN.role,
    }),
    { ...cookieOptions, maxAge: SESSION_MAX_AGE },
  );
  return response;
}
