import { NextResponse } from 'next/server';
import { ApiError, api, useMockData } from '@/lib/api/client';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

/**
 * The landing page at `/account/email/verify?token=...` reads the outcome
 * through here — public, since the token in the query string is what proves
 * identity, the same reasoning `/auth/password` rests the reset link on.
 * Mirrors `AccountVerificationEndpoints.ConfirmEmailVerification`: every
 * failure (unknown, expired, already-used, or a token for a stale address)
 * comes back indistinguishable, so there is exactly one Persian message for
 * "no" and one for "yes".
 */
export async function GET(request: Request) {
  const limit = rateLimit(clientKey(request, 'email-verify-confirm'), 10, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const token = new URL(request.url).searchParams.get('token');
  if (!token) {
    return NextResponse.json({ error: 'لینک نامعتبر است.' }, { status: 400 });
  }

  if (useMockData) {
    return NextResponse.json({ ok: true });
  }

  try {
    await api.get('/account/email/verify/confirm', { query: { token } });
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 400) {
      return NextResponse.json(
        { error: 'این لینک نامعتبر یا منقضی شده است.' },
        { status: 400 },
      );
    }

    return NextResponse.json(
      { error: 'تایید ایمیل ممکن نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }

  return NextResponse.json({ ok: true });
}
