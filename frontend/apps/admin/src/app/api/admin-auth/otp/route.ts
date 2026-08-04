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
import { useMockData } from '@/lib/api/client';

/**
 * One-time-code sign-in for the panel — development only.
 *
 * Screen 91 offers this beside the password, but the API has no admin OTP
 * endpoint: `POST /auth/login` takes a password and is the only way in. So
 * this exists for local work and refuses outright against a real backend,
 * rather than minting a credential the API never issued.
 *
 * It previously did the latter. The code was a hardcoded `111111` with no
 * environment check of any kind, and the verify half signed an *owner* session
 * without looking at which phone the challenge belonged to — so any number
 * shaped like an Iranian mobile could ask for a code, send back the constant,
 * and take over the panel. Both halves are now gated on mock mode, the code
 * has to be configured with no default that works, and the challenge only
 * carries a usable hash for the configured operator's number.
 *
 * As with the password path, the code never reaches the browser and the
 * attempt counter lives inside the signed cookie.
 */


const MOCK_ADMIN = {
  id: 'au-1',
  name: 'مدیر سیستم',
  email: 'admin@bojan.com',
  role: 'owner' as AdminRole,
};

/** Same message for an unknown number as for a known one. */
const ACCEPTED = { ok: true, resendAfter: 120 };

/**
 * What the operator sees when this route cannot sign anyone in.
 *
 * Deliberately about the method rather than the credential: it is returned
 * when the flow is unavailable, not when a code was wrong, and telling someone
 * their code was incorrect when none was ever going to work sends them round
 * the loop again.
 */
const UNAVAILABLE = 'ورود با کد یک‌بارمصرف در دسترس نیست. با رمز عبور وارد شوید.';

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

    // There is no admin one-time-code endpoint on the API — `POST /auth/login`
    // is the only way in, and it takes a password. Against a real backend this
    // flow has nothing to delegate to, and a code minted here would be a
    // credential this app invented for itself.
    if (!useMockData) {
      console.error('[admin-auth] OTP sign-in has no backend endpoint; use the password form.');
      return NextResponse.json({ error: UNAVAILABLE }, { status: 401 });
    }

    // Never a default that works. The password path already refuses to sign
    // anyone in until ADMIN_DEV_PASSWORD is set, for the same reason: this used
    // to fall back to a fixed "111111", which meant any phone number shaped
    // like a mobile could request a code, send that constant back, and be
    // handed an owner session.
    const configured = process.env.ADMIN_DEV_OTP;
    if (!configured) {
      console.error('[admin-auth] ADMIN_DEV_OTP is not set; one-time-code sign-in is disabled.');
      return NextResponse.json({ error: UNAVAILABLE }, { status: 401 });
    }

    // The phone has to be the configured operator's, the way the password path
    // checks the identity against ADMIN_DEV_EMAIL. A number that does not match
    // still gets the same answer and a challenge — one whose hash is a random
    // value no submitted code can equal — so this cannot be used to find out
    // which number belongs to an operator.
    const expectedPhone = normalizeDigitsInput(process.env.ADMIN_DEV_PHONE ?? '');
    const known = expectedPhone.length > 0 && phone === expectedPhone;

    if (process.env.NODE_ENV !== 'production' && known) {
      console.info(`[admin-auth] mock OTP for ${phone}: ${configured}`);
    }

    const response = NextResponse.json(ACCEPTED);
    response.cookies.set(
      OTP_COOKIE,
      await signOtpChallenge({
        phone,
        codeHash: await hashSecret(known ? configured : crypto.randomUUID()),
        attempts: 0,
      }),
      { ...cookieOptions, maxAge: OTP_MAX_AGE },
    );
    return response;
  }

  // Checked again on this half. The request path above refuses outside mock
  // mode, so no challenge can be issued — but a cookie minted while the app was
  // in mock mode stays valid for OTP_MAX_AGE and is signed with the same
  // AUTH_SECRET, and the thing on the other side of this check is an owner
  // session. One line against a five-minute window worth a total compromise.
  if (!useMockData) {
    return NextResponse.json({ error: UNAVAILABLE }, { status: 401 });
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
