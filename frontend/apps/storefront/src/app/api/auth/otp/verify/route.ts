import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { ApiError, api, useMockData } from '@/lib/api/client';
import { mockUser } from '@/lib/mock/catalog';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import {
  OTP_COOKIE,
  OTP_MAX_ATTEMPTS,
  SESSION_COOKIE,
  SESSION_MAX_AGE,
  cookieOptions,
  hashCode,
  signOtpChallenge,
  signSession,
  verifyOtpChallenge,
} from '@/lib/auth/session';

/**
 * Screen 51 — verify the SMS code and open a session.
 *
 * A wrong code costs an attempt; the fifth one burns the challenge and the
 * customer has to request a new code. The attempt counter lives inside the
 * signed cookie, so it cannot be reset by clearing local state.
 */

interface VerifyResponse {
  userId: string;
  firstName?: string;
  lastName?: string;
  isNewUser?: boolean;
  /** Bearer token for later calls. Absent in mock mode, where there is no API. */
  token?: string;
}

/**
 * The machine key the backend puts in a ProblemDetails `title`.
 *
 * `otp-expired`, `otp-attempts-exhausted` or `otp-incorrect` — sentences are
 * this app's job, so the API sends a key and never Persian copy.
 */
function apiErrorKey(error: ApiError): string | null {
  const body = error.body;
  if (!body || typeof body !== 'object') return null;
  const title = (body as { title?: unknown }).title;
  return typeof title === 'string' ? title : null;
}

function clearChallenge(response: NextResponse): NextResponse {
  response.cookies.set(OTP_COOKIE, '', { ...cookieOptions, maxAge: 0 });
  return response;
}

export async function POST(request: Request) {
  const limit = rateLimit(clientKey(request, 'otp-verify'), 10, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'تلاش‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const store = await cookies();
  const challenge = await verifyOtpChallenge(store.get(OTP_COOKIE)?.value);

  if (!challenge) {
    return clearChallenge(
      NextResponse.json({ error: 'کد تایید منقضی شده است. دوباره درخواست دهید.' }, { status: 400 }),
    );
  }

  const body = (await request.json().catch(() => null)) as { code?: unknown } | null;
  const code = normalizeDigitsInput(String(body?.code ?? ''));

  if (code.length !== 5) {
    return NextResponse.json({ error: 'کد تایید ۵ رقمی را کامل وارد کنید.' }, { status: 400 });
  }

  /**
   * A wrong guess, whoever judged it.
   *
   * The counter lives in the signed cookie and is spent in both modes. The
   * backend keeps its own count against the challenge it issued — that is the
   * one that actually stops a brute force, since a caller who bypasses this
   * route never touches this cookie — but leaving the local counter to mock
   * mode meant the wider of the two windows was the only one in force wherever
   * the app really runs.
   */
  async function wrongCode(): Promise<NextResponse> {
    const attempts = challenge!.attempts + 1;

    if (attempts >= OTP_MAX_ATTEMPTS) {
      return clearChallenge(
        NextResponse.json(
          { error: 'تعداد تلاش‌ها بیش از حد مجاز است. کد تازه‌ای درخواست کنید.' },
          { status: 429 },
        ),
      );
    }

    const response = NextResponse.json(
      { error: 'کد تایید نادرست است.', remaining: OTP_MAX_ATTEMPTS - attempts },
      { status: 400 },
    );
    response.cookies.set(OTP_COOKIE, await signOtpChallenge({ ...challenge!, attempts }), {
      ...cookieOptions,
      // Keep the original deadline; a wrong guess must not extend the window.
      maxAge: Math.max(1, challenge!.exp - Math.floor(Date.now() / 1000)),
    });
    return response;
  }

  let account: VerifyResponse;

  if (useMockData) {
    if ((await hashCode(code)) !== challenge.codeHash) {
      return wrongCode();
    }

    // Mirrors the API's own rule: a number it has not seen before is a new
    // customer, and the caller is told so. Without this the mock always
    // answered as the fixture shopper, so `isNewUser` was never true and the
    // sign-up half of this screen — the profile step on 52 — could not be
    // reached or demonstrated locally at all.
    const known = challenge.phone === mockUser.phone;

    account = known
      ? {
          userId: mockUser.id,
          firstName: mockUser.firstName,
          lastName: mockUser.lastName,
        }
      : { userId: mockUser.id, isNewUser: true };
  } else {
    try {
      account = await api.post<VerifyResponse>('/auth/otp/verify', {
        phone: challenge.phone,
        code,
      });
    } catch (error) {
      // The backend distinguishes three failures and answers with a machine
      // key for each. Collapsing all of them into "wrong code" left the two
      // that end the challenge — it is spent, or it has expired — telling the
      // customer to try again against a challenge that can no longer succeed,
      // with no way to learn they needed a fresh code.
      const title = error instanceof ApiError ? apiErrorKey(error) : null;

      if (title === 'otp-attempts-exhausted') {
        return clearChallenge(
          NextResponse.json(
            { error: 'تعداد تلاش‌ها بیش از حد مجاز است. کد تازه‌ای درخواست کنید.' },
            { status: 429 },
          ),
        );
      }

      if (title === 'otp-expired') {
        return clearChallenge(
          NextResponse.json(
            { error: 'کد تایید منقضی شده است. دوباره درخواست دهید.' },
            { status: 400 },
          ),
        );
      }

      return wrongCode();
    }
  }

  const name = [account.firstName, account.lastName].filter(Boolean).join(' ').trim();

  const response = clearChallenge(
    NextResponse.json({ ok: true, isNewUser: account.isNewUser === true }),
  );

  response.cookies.set(
    SESSION_COOKIE,
    await signSession({
      sub: account.userId,
      phone: challenge.phone,
      ...(name ? { name } : null),
      ...(account.token ? { token: account.token } : null),
    }),
    { ...cookieOptions, maxAge: SESSION_MAX_AGE },
  );

  return response;
}
