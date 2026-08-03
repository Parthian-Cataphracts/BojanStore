import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import {
  LOGIN_MAX_ATTEMPTS,
  SESSION_COOKIE,
  SESSION_MAX_AGE,
  cookieOptions,
  hashSecret,
  signSession,
  type AdminRole,
} from '@/lib/auth/session';
import { useMockData } from '@/lib/api/client';

/**
 * Screen 91 — admin sign-in.
 *
 * The panel has no backend to authenticate against yet, so in mock mode a
 * single operator is configured through the environment. The important part is
 * what does *not* change when the backend arrives: the attempt limit, the
 * identical response for a wrong password and an unknown account, and the
 * http-only session cookie are all decided here.
 */

const MOCK_ADMIN = {
  id: 'au-1',
  name: 'مدیر سیستم',
  email: 'admin@bojan.com',
  role: 'owner' as AdminRole,
};

interface LoginResponse {
  id: string;
  name: string;
  email: string;
  role: AdminRole;
  /** Set when the account has 2FA on — screen 153 takes it from here. */
  requiresTwoFactor?: boolean;
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
    // No session until the second factor clears — screen 153 owns that step.
    return NextResponse.json({ ok: true, requiresTwoFactor: true });
  }

  const response = NextResponse.json({ ok: true, requiresTwoFactor: false });

  response.cookies.set(
    SESSION_COOKIE,
    await signSession({
      sub: account.id,
      name: account.name,
      email: account.email,
      role: account.role,
    }),
    { ...cookieOptions, maxAge: SESSION_MAX_AGE },
  );

  return response;
}
