import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { api, useMockData } from '@/lib/api/client';
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

  let account: VerifyResponse;

  if (useMockData) {
    if ((await hashCode(code)) !== challenge.codeHash) {
      const attempts = challenge.attempts + 1;

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
      response.cookies.set(OTP_COOKIE, await signOtpChallenge({ ...challenge, attempts }), {
        ...cookieOptions,
        // Keep the original deadline; a wrong guess must not extend the window.
        maxAge: Math.max(1, challenge.exp - Math.floor(Date.now() / 1000)),
      });
      return response;
    }

    account = {
      userId: mockUser.id,
      firstName: mockUser.firstName,
      lastName: mockUser.lastName,
    };
  } else {
    try {
      account = await api.post<VerifyResponse>('/auth/otp/verify', {
        phone: challenge.phone,
        code,
      });
    } catch {
      return NextResponse.json({ error: 'کد تایید نادرست است.' }, { status: 400 });
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
