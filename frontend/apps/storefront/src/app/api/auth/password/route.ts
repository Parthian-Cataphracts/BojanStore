import { NextResponse } from 'next/server';
import { toPersianDigits } from '@bojan/ui';
import { api, useMockData } from '@/lib/api/client';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

/**
 * Forgetting a password, and setting a new one.
 *
 * Both steps land here, chosen by `action`, because they are two halves of one
 * flow and neither opens a session — the customer signs in afterwards with the
 * password they just chose. That is deliberate: a reset link that also logged
 * you in would turn a forwarded email into an account takeover.
 */

const MIN_PASSWORD = 8;
const MAX_PASSWORD = 256;

/**
 * The same answer whether or not the address has an account.
 *
 * A different one turns this into a way to ask the shop "does this person
 * have an account here?" for any address someone cares to try.
 */
const SENT = {
  ok: true,
  message: 'اگر این ایمیل در بوژان ثبت شده باشد، لینک بازیابی برایتان ارسال شد.',
};

export async function POST(request: Request) {
  const body = (await request.json().catch(() => null)) as {
    action?: unknown;
    email?: unknown;
    token?: unknown;
    password?: unknown;
  } | null;

  const action = body?.action === 'reset' ? 'reset' : 'forgot';

  if (action === 'forgot') {
    const limit = rateLimit(clientKey(request, 'forgot-password'), 5, 300);
    if (!limit.allowed) {
      return NextResponse.json(
        { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
        { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
      );
    }

    const email = typeof body?.email === 'string' ? body.email.trim() : '';
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length > 200) {
      return NextResponse.json({ error: 'ایمیل معتبر وارد کنید.' }, { status: 400 });
    }

    if (!useMockData) {
      // Failures are swallowed on purpose. A 502 here would tell the caller
      // their address reached a real lookup, which is the one thing the
      // uniform answer above exists to hide.
      await api.post('/auth/forgot-password', { email }).catch(() => undefined);
    }

    return NextResponse.json(SENT);
  }

  const limit = rateLimit(clientKey(request, 'reset-password'), 10, 300);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'تلاش‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const token = typeof body?.token === 'string' ? body.token.trim() : '';
  const password = typeof body?.password === 'string' ? body.password : '';

  if (token.length === 0 || token.length > 128) {
    return NextResponse.json(
      { error: 'این لینک معتبر نیست یا منقضی شده است.' },
      { status: 400 },
    );
  }

  if (password.length < MIN_PASSWORD || password.length > MAX_PASSWORD) {
    return NextResponse.json(
      { error: `رمز عبور باید حداقل ${toPersianDigits(MIN_PASSWORD)} نویسه باشد.` },
      { status: 400 },
    );
  }

  if (!/[a-zA-Z؀-ۿ]/.test(password) || !/\d/.test(password)) {
    return NextResponse.json({ error: 'رمز عبور باید ترکیبی از حرف و عدد باشد.' }, { status: 400 });
  }

  if (useMockData) {
    // There is no token store to check against locally, and accepting anything
    // would make this form look like it worked.
    return NextResponse.json(
      { error: 'بازیابی رمز عبور در حالت نمایشی در دسترس نیست.' },
      { status: 400 },
    );
  }

  try {
    await api.post('/auth/reset-password', { token, password });
  } catch {
    // Expired, already used, and never existed are one answer — the API does
    // not distinguish them either, so a live token cannot be probed for.
    return NextResponse.json(
      { error: 'این لینک معتبر نیست یا منقضی شده است. دوباره درخواست دهید.' },
      { status: 400 },
    );
  }

  return NextResponse.json({ ok: true });
}
