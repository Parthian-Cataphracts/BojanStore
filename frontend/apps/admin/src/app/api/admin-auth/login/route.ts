import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import {
  LOGIN_MAX_ATTEMPTS,
  SESSION_COOKIE,
  SESSION_MAX_AGE,
  TWO_FACTOR_COOKIE,
  TWO_FACTOR_MAX_AGE,
  cookieOptions,
  hashSecret,
  signSession,
  signTwoFactorChallenge,
  type AdminRole,
} from '@/lib/auth/session';
import { useMockData } from '@/lib/api/mock-data';

/**
 * Screen 91 — admin sign-in, first factor.
 *
 * Against the API this forwards the credentials and writes the session from
 * what comes back. In mock mode a single operator is configured through the
 * environment, for local work before the backend is up. What does not change
 * between the two: the attempt limit, the identical response for a wrong
 * password and an unknown account, the http-only session cookie, and the rule
 * that an account with a second factor gets no session here at all.
 */

const MOCK_ADMIN = {
  id: 'au-1',
  name: 'مدیر سیستم',
  email: 'admin@bojan.com',
  role: 'owner' as AdminRole,
  // Fixed rather than random: mock mode has no account to rotate it against,
  // and a fresh value each sign-in would only look like revocation working.
  securityStamp: '00000000-0000-0000-0000-000000000001',
};

interface LoginResponse {
  id: string;
  name: string;
  email: string;
  role: AdminRole;
  /** Set when the account has 2FA on — `../two-factor` takes it from here. */
  requiresTwoFactor?: boolean;
  /**
   * Short-lived proof the password was accepted, present only alongside
   * `requiresTwoFactor`. It opens nothing on its own; `../two-factor` trades it
   * for a session once a code verifies.
   */
  challenge?: string;
  /** The account's revocation stamp — absent alongside `challenge`. */
  securityStamp?: string;
  /**
   * The password was set by somebody else — an owner appointing this operator,
   * or resetting them after a lockout. Sent only when true, so its absence is
   * the ordinary case.
   */
  mustChangePassword?: boolean;
  /**
   * The sections and screens this operator is narrowed to. Empty or absent
   * means not narrowed — see `lib/permissions`.
   */
  sections?: string[];
}

/** One message for every failure: a distinct one would confirm valid accounts. */
const REJECTED = 'نام کاربری یا رمز عبور نادرست است.';

export async function POST(request: Request) {
  const limit = rateLimit(clientKey(request, 'admin-login'), LOGIN_MAX_ATTEMPTS, 300);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'تلاش‌های ناموفق بیش از حد. چند دقیقه بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const body = (await request.json().catch(() => null)) as {
    identity?: unknown;
    password?: unknown;
  } | null;

  const identity = typeof body?.identity === 'string' ? body.identity.trim() : '';
  const password = typeof body?.password === 'string' ? body.password : '';

  const isPhone = /^09\d{9}$/.test(normalizeDigitsInput(identity));
  const isEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(identity);

  if ((!isPhone && !isEmail) || password.length < 8 || password.length > 200) {
    return NextResponse.json({ error: REJECTED }, { status: 401 });
  }

  let account: LoginResponse;

  if (useMockData) {
    const expectedEmail = process.env.ADMIN_DEV_EMAIL ?? MOCK_ADMIN.email;
    const expectedPassword = process.env.ADMIN_DEV_PASSWORD;

    if (!expectedPassword) {
      // No configured password means no way in — never a default that works.
      console.error('[admin-auth] ADMIN_DEV_PASSWORD is not set; sign-in is disabled.');
      return NextResponse.json({ error: REJECTED }, { status: 401 });
    }

    const matches =
      identity.toLowerCase() === expectedEmail.toLowerCase() &&
      (await hashSecret(password)) === (await hashSecret(expectedPassword));

    if (!matches) {
      return NextResponse.json({ error: REJECTED }, { status: 401 });
    }

    account = MOCK_ADMIN;
  } else {
    try {
      const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
      if (!base) throw new Error('API base URL is not configured.');

      const upstream = await fetch(`${base.replace(/\/$/, '')}/auth/login`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json',
          ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
        },
        body: JSON.stringify({ identity, password }),
        cache: 'no-store',
      });

      if (!upstream.ok) return NextResponse.json({ error: REJECTED }, { status: 401 });
      account = (await upstream.json()) as LoginResponse;
    } catch {
      return NextResponse.json(
        { error: 'ارتباط با سرور برقرار نشد. کمی بعد دوباره تلاش کنید.' },
        { status: 502 },
      );
    }
  }

  if (account.requiresTwoFactor) {
    // No session until the second factor clears. The challenge travels in its
    // own http-only cookie rather than back to the browser: it is the only
    // thing standing between a stolen password and the panel, and a value the
    // page can read is a value a script on it can take.
    if (!account.challenge) {
      // The API asked for a second factor and gave nothing to complete it
      // with. Signing the operator in anyway is the one response that must not
      // happen here.
      console.error(
        '[admin-auth] requiresTwoFactor was set without a challenge; refusing sign-in.',
      );
      return NextResponse.json({ error: REJECTED }, { status: 401 });
    }

    const pending = NextResponse.json({ ok: true, requiresTwoFactor: true });
    pending.cookies.set(
      TWO_FACTOR_COOKIE,
      await signTwoFactorChallenge({ challenge: account.challenge, attempts: 0 }),
      { ...cookieOptions, maxAge: TWO_FACTOR_MAX_AGE },
    );
    return pending;
  }

  if (!account.securityStamp) {
    // Every authorised call carries this back to the API, which refuses the
    // ones that do not. A session signed without it would be a cookie the
    // browser holds and no endpoint accepts.
    console.error(
      '[admin-auth] the API returned a session without a securityStamp; refusing sign-in.',
    );
    return NextResponse.json({ error: REJECTED }, { status: 401 });
  }

  const response = NextResponse.json({ ok: true, requiresTwoFactor: false });

  response.cookies.set(
    SESSION_COOKIE,
    await signSession({
      sub: account.id,
      name: account.name,
      email: account.email,
      role: account.role,
      stamp: account.securityStamp,
      // Carried into the cookie so the middleware can act on it without a
      // request to the API per page. The panel holds the operator on the
      // change-password screen while it is set.
      ...(account.mustChangePassword ? { mustChangePassword: true } : null),
      // Same reasoning, and the menu cannot be drawn without it. Absent when
      // the operator is not narrowed, which is the ordinary case.
      ...(account.sections?.length ? { sections: account.sections } : null),
    }),
    { ...cookieOptions, maxAge: SESSION_MAX_AGE },
  );

  return response;
}
