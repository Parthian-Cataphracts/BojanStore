import { NextResponse } from 'next/server';
import { normalizeDigitsInput, toPersianDigits } from '@bojan/ui';
import { ApiError, api, useMockData } from '@/lib/api/client';
import { mockUser } from '@/lib/mock/catalog';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import { issueSession, type AuthenticatedAccount } from '@/lib/auth/issue-session';

/**
 * Screen 10 — register with a phone, an email and a password.
 *
 * The other half of sign-in rather than a different system: the API answers in
 * the same shape `/auth/otp/verify` does, so the session that comes out of this
 * is the session that comes out of a code.
 *
 * A password exists because SMS is the unreliable part. Somebody whose code
 * never arrives has to have a way in that does not depend on a text message
 * arriving, and that means an account they can set up in one go.
 */

/** Mirrors the API's PasswordPolicy so the form can say why before a round trip. */
const MIN_PASSWORD = 8;
const MAX_PASSWORD = 256;

export async function POST(request: Request) {
  const limit = rateLimit(clientKey(request, 'register'), 5, 300);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const body = (await request.json().catch(() => null)) as {
    phone?: unknown;
    email?: unknown;
    password?: unknown;
  } | null;

  const phone = normalizeDigitsInput(String(body?.phone ?? ''));
  const email = typeof body?.email === 'string' ? body.email.trim() : '';
  const password = typeof body?.password === 'string' ? body.password : '';

  if (!/^09\d{9}$/.test(phone)) {
    return NextResponse.json(
      { error: 'شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود.' },
      { status: 400 },
    );
  }

  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length > 200) {
    return NextResponse.json({ error: 'ایمیل معتبر وارد کنید.' }, { status: 400 });
  }

  // Checked here as well as in the API: the same rejection arrives faster, and
  // the ceiling keeps an oversized body from reaching PBKDF2 at all.
  if (password.length < MIN_PASSWORD || password.length > MAX_PASSWORD) {
    return NextResponse.json(
      { error: `رمز عبور باید حداقل ${toPersianDigits(MIN_PASSWORD)} نویسه باشد.` },
      { status: 400 },
    );
  }

  if (!/[a-zA-Z؀-ۿ]/.test(password) || !/\d/.test(password)) {
    return NextResponse.json(
      { error: 'رمز عبور باید ترکیبی از حرف و عدد باشد.' },
      { status: 400 },
    );
  }

  let account: AuthenticatedAccount;

  if (useMockData) {
    account = { userId: mockUser.id, isNewUser: true };
  } else {
    try {
      account = await api.post<AuthenticatedAccount>('/auth/register', { phone, email, password });
    } catch (cause) {
      // 409 is the API's answer for a phone or an email that already has an
      // account. Which of the two it was is not said — either way the person
      // in front of this form should be signing in, not registering.
      if (cause instanceof ApiError && cause.status === 409) {
        return NextResponse.json(
          { error: 'این شماره یا ایمیل قبلاً ثبت شده است. وارد شوید.' },
          { status: 409 },
        );
      }

      return NextResponse.json(
        { error: 'ثبت‌نام انجام نشد. کمی بعد دوباره تلاش کنید.' },
        { status: 502 },
      );
    }
  }

  return issueSession(NextResponse.json({ ok: true, isNewUser: true }), account, phone);
}
