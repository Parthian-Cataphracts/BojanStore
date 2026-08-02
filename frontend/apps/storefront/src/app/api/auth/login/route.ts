import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { api, useMockData } from '@/lib/api/client';
import { mockUser } from '@/lib/mock/catalog';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import { issueSession, type AuthenticatedAccount } from '@/lib/auth/issue-session';

/**
 * Screen 09's second door — sign in with a password instead of a code.
 *
 * The identity is a phone number or an email and the form does not ask which:
 * somebody who registered months ago should not have to remember what they
 * typed into which box.
 */

/** One message for every failure — see the API's own note on why. */
const REJECTED = 'شماره/ایمیل یا رمز عبور نادرست است.';

export async function POST(request: Request) {
  const limit = rateLimit(clientKey(request, 'password-login'), 10, 300);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'تلاش‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const body = (await request.json().catch(() => null)) as {
    identity?: unknown;
    password?: unknown;
  } | null;

  const raw = typeof body?.identity === 'string' ? body.identity.trim() : '';
  const password = typeof body?.password === 'string' ? body.password : '';

  if (raw.length === 0 || raw.length > 200 || password.length === 0 || password.length > 256) {
    return NextResponse.json({ error: REJECTED }, { status: 401 });
  }

  // A phone typed with Persian digits is the same phone. An email is left as
  // it was typed — the API lower-cases it.
  const identity = raw.includes('@') ? raw : normalizeDigitsInput(raw);

  let account: AuthenticatedAccount;

  if (useMockData) {
    // No password is stored anywhere in mock mode, so there is nothing to
    // check against and pretending otherwise would make this form look like it
    // worked. Only the fixture shopper's own identity is accepted, and the
    // password is required to be *something* so the field is not decorative.
    const known = identity === mockUser.phone || identity === mockUser.email?.toLowerCase();

    if (!known || password.length < 8) {
      return NextResponse.json({ error: REJECTED }, { status: 401 });
    }

    account = {
      userId: mockUser.id,
      firstName: mockUser.firstName,
      lastName: mockUser.lastName,
    };
  } else {
    try {
      account = await api.post<AuthenticatedAccount>('/auth/login', { identity, password });
    } catch {
      // Unknown identity, no password on the account, wrong password — the API
      // answers 401 for all three and so does this. The difference is what
      // someone enumerating the shop's customers would be looking for.
      return NextResponse.json({ error: REJECTED }, { status: 401 });
    }
  }

  // The session stores a phone, and signing in by email does not supply one —
  // so the API returns the account's. Falling back to what was typed only
  // covers the phone case; there is no guessing involved either way.
  const phone = account.phone ?? (identity.includes('@') ? '' : identity);

  return issueSession(NextResponse.json({ ok: true, isNewUser: false }), account, phone);
}
