import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { api, useMockData } from '@/lib/api/client';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import {
  OTP_COOKIE,
  OTP_MAX_AGE,
  cookieOptions,
  hashCode,
  signOtpChallenge,
} from '@/lib/auth/session';

/**
 * Screen 09 — request an SMS code.
 *
 * The code never reaches the browser. It is hashed into a signed, http-only
 * challenge cookie which `../verify` checks; in mock mode the code is printed
 * to the server console the way a local SMS gateway stub would.
 */

/** The design's resend timer. */
const RESEND_AFTER_SECONDS = 120;

export async function POST(request: Request) {
  // Two windows: a tight one to stop a single number being spammed, and a
  // wider one to stop a script walking through many numbers from one address.
  const burst = rateLimit(clientKey(request, 'otp-request'), 5, 60);
  const sustained = rateLimit(clientKey(request, 'otp-request-hour'), 20, 3600);

  if (!burst.allowed || !sustained.allowed) {
    const retryAfter = Math.max(burst.retryAfter, sustained.retryAfter);
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(retryAfter) } },
    );
  }

  const body = (await request.json().catch(() => null)) as { phone?: unknown } | null;
  const phone = normalizeDigitsInput(String(body?.phone ?? ''));

  if (!/^09\d{9}$/.test(phone)) {
    return NextResponse.json(
      { error: 'شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.' },
      { status: 400 },
    );
  }

  let code: string | null = null;

  if (useMockData) {
    // A fixed code stands in for the SMS gateway. Configurable so it is never
    // a constant an attacker could rely on if this ever ran outside local dev.
    code = process.env.MOCK_OTP_CODE ?? '11111';
    if (process.env.NODE_ENV !== 'production') {
      console.info(`[auth] mock OTP for ${phone}: ${code}`);
    }
  } else {
    try {
      await api.post('/auth/otp/request', { phone });
    } catch {
      // Never echo the upstream error — it would confirm whether the number is
      // registered. The client sees the same response either way.
      return NextResponse.json(
        { error: 'ارسال کد تایید ممکن نشد. کمی بعد دوباره تلاش کنید.' },
        { status: 502 },
      );
    }
  }

  const response = NextResponse.json({ ok: true, resendAfter: RESEND_AFTER_SECONDS });

  response.cookies.set(
    OTP_COOKIE,
    await signOtpChallenge({
      phone,
      // In real mode the backend holds the code; the hash of a random value
      // keeps the cookie shape identical without ever matching a user entry.
      codeHash: await hashCode(code ?? crypto.randomUUID()),
      attempts: 0,
    }),
    { ...cookieOptions, maxAge: OTP_MAX_AGE },
  );

  return response;
}
