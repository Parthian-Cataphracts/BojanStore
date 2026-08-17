import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import {
  SESSION_COOKIE,
  SESSION_MAX_AGE,
  TWO_FACTOR_COOKIE,
  TWO_FACTOR_MAX_ATTEMPTS,
  cookieOptions,
  signSession,
  signTwoFactorChallenge,
  verifyTwoFactorChallenge,
  type AdminRole,
} from '@/lib/auth/session';

/**
 * The second half of a two-factor sign-in.
 *
 * Reachable only with the challenge cookie `../login` sets after a password is
 * accepted — there is no path here that starts from nothing, and no code this
 * route judges on its own. The code goes to the API, which holds the operator's
 * TOTP secret and is the only thing that can say whether it is right; the
 * session cookie is written from what comes back, never from what was sent.
 */

interface TwoFactorResponse {
  id: string;
  name: string;
  email: string;
  role: AdminRole;
  /** The account's revocation stamp — see `AdminSession.stamp`. */
  securityStamp?: string;
  /**
   * The password was set by somebody else. Carried here as well as on the
   * password step: an operator whose owner reset them may well have a second
   * factor, and a sign-in that finishes on this route would otherwise walk past
   * the requirement the other route enforces.
   */
  mustChangePassword?: boolean;
  /**
   * What this operator is narrowed to. Carried here as well as on the password
   * step for the same reason `mustChangePassword` is: a sign-in that finishes
   * on this route is a sign-in, and the menu it lands on has to be theirs.
   */
  sections?: string[];
}

/** One message for every failure, as on the password step. */
const REJECTED = 'کد تایید نادرست است.';

const EXPIRED = 'مهلت تایید دو مرحله‌ای به پایان رسید. دوباره وارد شوید.';

function clearChallenge(response: NextResponse): NextResponse {
  response.cookies.set(TWO_FACTOR_COOKIE, '', { ...cookieOptions, maxAge: 0 });
  return response;
}

export async function POST(request: Request) {
  const limit = rateLimit(clientKey(request, 'admin-2fa'), 10, 300);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'تلاش‌های بیش از حد. چند دقیقه بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const store = await cookies();
  const pending = await verifyTwoFactorChallenge(store.get(TWO_FACTOR_COOKIE)?.value);

  if (!pending) {
    return clearChallenge(NextResponse.json({ error: EXPIRED }, { status: 401 }));
  }

  const body = (await request.json().catch(() => null)) as { code?: unknown } | null;
  const code = normalizeDigitsInput(String(body?.code ?? ''));

  if (!/^\d{6}$/.test(code)) {
    return NextResponse.json({ error: 'کد تایید ۶ رقمی را کامل وارد کنید.' }, { status: 400 });
  }

  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  let account: TwoFactorResponse;

  try {
    const upstream = await fetch(`${base.replace(/\/$/, '')}/auth/2fa`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
      },
      body: JSON.stringify({ challenge: pending.challenge, code }),
      cache: 'no-store',
    });

    if (!upstream.ok) {
      const attempts = pending.attempts + 1;

      // The API keeps its own limit against the challenge; this one burns the
      // cookie so a spent challenge cannot be retried from this side either.
      if (attempts >= TWO_FACTOR_MAX_ATTEMPTS) {
        return clearChallenge(
          NextResponse.json(
            { error: 'تعداد تلاش‌ها بیش از حد مجاز است. دوباره وارد شوید.' },
            { status: 429 },
          ),
        );
      }

      const wrong = NextResponse.json({ error: REJECTED }, { status: 401 });
      wrong.cookies.set(
        TWO_FACTOR_COOKIE,
        await signTwoFactorChallenge({ ...pending, attempts }, pending.exp),
        { ...cookieOptions, maxAge: Math.max(1, pending.exp - Math.floor(Date.now() / 1000)) },
      );
      return wrong;
    }

    account = (await upstream.json()) as TwoFactorResponse;
  } catch {
    return NextResponse.json(
      { error: 'ارتباط با سرور برقرار نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }

  if (!account.securityStamp) {
    console.error(
      '[admin-auth] the API completed a second factor without a securityStamp; refusing sign-in.',
    );
    return clearChallenge(NextResponse.json({ error: REJECTED }, { status: 401 }));
  }

  const response = clearChallenge(NextResponse.json({ ok: true }));

  response.cookies.set(
    SESSION_COOKIE,
    await signSession({
      sub: account.id,
      name: account.name,
      email: account.email,
      role: account.role,
      stamp: account.securityStamp,
      ...(account.mustChangePassword ? { mustChangePassword: true } : null),
      ...(account.sections?.length ? { sections: account.sections } : null),
    }),
    { ...cookieOptions, maxAge: SESSION_MAX_AGE },
  );

  return response;
}
