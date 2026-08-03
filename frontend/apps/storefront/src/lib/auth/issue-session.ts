import { NextResponse } from 'next/server';
import { SESSION_COOKIE, SESSION_MAX_AGE, cookieOptions, signSession } from './session';

/**
 * What every way into an account answers with.
 *
 * The API returns this same shape from `/auth/otp/verify`, `/auth/register` and
 * `/auth/login`, so the three routes that call them differ only in which
 * endpoint they hit — not in what they do with the answer.
 */
export interface AuthenticatedAccount {
  userId: string;
  firstName?: string;
  lastName?: string;
  isNewUser?: boolean;
  /** Bearer token the API issued; forwarded on later calls made for this customer. */
  token?: string;
  /**
   * The account's phone. Only the password endpoints supply it, because only
   * they can be reached without the caller already knowing the number.
   */
  phone?: string;
  /** The account's security stamp — see `SessionPayload.stamp`. */
  securityStamp?: string;
}

/**
 * Puts the session cookie on a response.
 *
 * Extracted because there are now three doors into an account — a one-time
 * code, a password, and registering — and a session minted slightly differently
 * by one of them is the kind of difference nobody notices until a customer is
 * signed in without a name, or with a token the data layer never forwards.
 */
export async function issueSession(
  response: NextResponse,
  account: AuthenticatedAccount,
  phone: string,
): Promise<NextResponse> {
  const name = [account.firstName, account.lastName].filter(Boolean).join(' ').trim();

  response.cookies.set(
    SESSION_COOKIE,
    await signSession({
      sub: account.userId,
      phone,
      ...(name ? { name } : null),
      ...(account.token ? { token: account.token } : null),
      ...(account.securityStamp ? { stamp: account.securityStamp } : null),
    }),
    { ...cookieOptions, maxAge: SESSION_MAX_AGE },
  );

  return response;
}
