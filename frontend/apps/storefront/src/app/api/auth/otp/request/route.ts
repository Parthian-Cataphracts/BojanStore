import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { ApiError, api, useMockData } from '@/lib/api/client';
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

/**
 * The design's resend timer, and the backend's own `OtpChallenge.Lifetime` —
 * the two have to agree, because this is what a shopper sees counting down
 * and that is what actually decides when a second request stops being
 * refused. Nothing here enforces the number; it exists so the countdown
 * shown before a real answer comes back is the right one instead of a guess
 * that is wrong for two minutes.
 */
const RESEND_AFTER_SECONDS = 120;

/**
 * The machine key the backend puts in a ProblemDetails `title` when a phone
 * already has a live code — see `AuthEndpoints.RequestOtp`.
 */
const OTP_COOLDOWN_KEY = 'otp-cooldown';

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
    } catch (cause) {
      // The one upstream answer worth echoing: a phone already has a live
      // code, so this is not a failure to explain away — it is the state a
      // shopper acted on a moment ago, most often by reloading the page or
      // going back to the phone step, neither of which cleared anything on
      // the backend. Reusing `submitErrorMessage`'s own 429 sentence keeps
      // this consistent with every other cooldown message in the app, and
      // forwarding Retry-After is what lets the OTP step, not just this
      // route, know precisely how long is left.
      const retryAfterSeconds =
        cause instanceof ApiError &&
        cause.status === 429 &&
        typeof cause.body === 'object' &&
        cause.body !== null &&
        (cause.body as { title?: unknown }).title === OTP_COOLDOWN_KEY &&
        typeof (cause.body as { retryAfterSeconds?: unknown }).retryAfterSeconds === 'number'
          ? (cause.body as { retryAfterSeconds: number }).retryAfterSeconds
          : null;

      if (retryAfterSeconds !== null) {
        return NextResponse.json(
          {
            error: `کد قبلی هنوز معتبر است. ${retryAfterSeconds} ثانیه دیگر می‌توانید کد تازه‌ای بگیرید.`,
            resendAfter: retryAfterSeconds,
          },
          { status: 429, headers: { 'Retry-After': String(retryAfterSeconds) } },
        );
      }

      // Every other upstream failure stays unexplained — echoing it could
      // confirm whether the number is registered. The client sees the same
      // response either way.
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
