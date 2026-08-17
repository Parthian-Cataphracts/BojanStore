/**
 * The operator's signed session cookie, and the two half-finished sign-ins that
 * lead to it.
 *
 * Same envelope as the storefront's — `@bojan/config/signed-cookie` — with a
 * different cookie name, a different secret, and a different payload: the panel
 * needs the operator's role to decide what they may see, and a much shorter
 * lifetime than a shopper's session.
 */

import { createSignedCookieCodec, isExpired, sha256Hex } from '@bojan/config/signed-cookie';

export const SESSION_COOKIE = 'bojan_admin_session';
export const OTP_COOKIE = 'bojan_admin_otp';

/** Where the password step parks its receipt while the second factor is entered. */
export const TWO_FACTOR_COOKIE = 'bojan_admin_2fa';

/** Eight hours — a working day, not a month. */
export const SESSION_MAX_AGE = 60 * 60 * 8;

/** An OTP challenge is short-lived. */
export const OTP_MAX_AGE = 60 * 5;

/**
 * How long the half-finished sign-in survives. Matches the API's own
 * `Jwt:TwoFactorChallengeLifetime`, so the cookie and the token inside it
 * expire together rather than one outliving the other.
 */
export const TWO_FACTOR_MAX_AGE = 60 * 5;

/** Wrong codes allowed before the two-factor challenge is burned. */
export const TWO_FACTOR_MAX_ATTEMPTS = 5;

/** Wrong codes allowed before the challenge is burned. */
export const OTP_MAX_ATTEMPTS = 5;

/** Failed sign-ins allowed per address before the form stops accepting any. */
export const LOGIN_MAX_ATTEMPTS = 8;

export interface OtpChallenge {
  phone: string;
  /** Hex SHA-256 of the code — never the code itself. */
  codeHash: string;
  attempts: number;
  exp: number;
}

/**
 * The password step's receipt, held between the two halves of a two-factor
 * sign-in.
 *
 * `challenge` is opaque here on purpose: it is the backend's own short-lived
 * token, and this app neither mints nor inspects it. Wrapping it in a signed
 * cookie keeps it out of the browser's reach and lets the attempt counter ride
 * along where a client cannot reset it.
 */
export interface TwoFactorChallenge {
  challenge: string;
  attempts: number;
  exp: number;
}

export type AdminRole = 'owner' | 'product' | 'sales' | 'support';

export interface AdminSession {
  sub: string;
  name: string;
  email: string;
  role: AdminRole;
  /**
   * The account's security stamp at sign-in, sent back to the API on every
   * request as `X-Admin-Stamp`.
   *
   * A signed cookie is only as revocable as whatever it is checked against, and
   * this one lasts a working day. Without the stamp, changing a password — the
   * thing an operator does *because* someone else has their session — left that
   * session open until it expired on its own.
   */
  stamp: string;
  /**
   * The password behind this session was chosen by somebody else — an owner
   * appointing this operator, or resetting them after a lockout.
   *
   * The middleware holds them on the change-password screen until it is gone.
   * It rides in the cookie rather than being fetched per request because it is
   * a fact that changes once in an account's life, and the cookie is minted at
   * exactly the moment it is known. The cost is that it can only become false
   * where the cookie is rewritten — which is the change-password form, the one
   * place it is allowed to become false at all.
   */
  mustChangePassword?: boolean;
  /**
   * The sections and screens this operator has been narrowed to, or absent when
   * they have not been — which means the whole of what their role allows.
   *
   * In the cookie rather than fetched per page because the menu needs it before
   * it can draw a single row, and a request per page for an answer that changes
   * about once in an operator's employment is a request per page. It is safe to
   * carry for the cookie's whole life only because changing somebody's grants
   * rotates their security stamp, and a stamp the API no longer recognises ends
   * the session on its next request. So a stale list here cannot outlive the
   * permissions it describes; it can only fail closed.
   */
  sections?: string[];
  /** Expiry, unix seconds. */
  exp: number;
}

export const cookieOptions = {
  httpOnly: true,
  // Stricter than the storefront: the panel has no payment-gateway round trip
  // to come back from, so there is no reason to accept cross-site navigation.
  //
  // What Strict costs elsewhere is the operator who clicks a link to the panel
  // in an email and lands on the sign-in screen despite holding a valid
  // session. Nothing this shop sends contains such a link — no notification,
  // no report, no alert addresses the panel at all — so the cost here is zero
  // and stays zero only while that is true. Anything that starts mailing
  // operators a deep link into the panel has to move this to 'lax' in the same
  // change, or it will be reported as a session that keeps logging people out.
  sameSite: 'strict',
  secure: process.env.NODE_ENV === 'production',
  path: '/',
} as const;

const DEV_SECRET = 'bojan-admin-development-secret-do-not-use-in-production';

function secretBytes(): Uint8Array {
  const configured = process.env.AUTH_SECRET;

  if (!configured || configured.length < 32) {
    if (process.env.NODE_ENV === 'production') {
      throw new Error(
        'AUTH_SECRET is missing or shorter than 32 characters. Admin sessions cannot be signed.',
      );
    }
    return new TextEncoder().encode(DEV_SECRET);
  }

  return new TextEncoder().encode(configured);
}

const codec = createSignedCookieCodec(secretBytes);

export async function signSession(payload: Omit<AdminSession, 'exp'>): Promise<string> {
  return codec.sign({ ...payload, exp: Math.floor(Date.now() / 1000) + SESSION_MAX_AGE });
}

export async function verifySession(token: string | undefined): Promise<AdminSession | null> {
  const payload = await codec.verify<AdminSession>(token);
  if (!payload || isExpired(payload.exp)) return null;
  if (typeof payload.sub !== 'string' || typeof payload.role !== 'string') return null;
  // A cookie minted before the stamp existed is refused rather than carried:
  // it is exactly the credential revocation cannot reach.
  if (typeof payload.stamp !== 'string' || payload.stamp.length === 0) return null;
  return payload;
}

export async function signOtpChallenge(challenge: Omit<OtpChallenge, 'exp'>): Promise<string> {
  return codec.sign({ ...challenge, exp: Math.floor(Date.now() / 1000) + OTP_MAX_AGE });
}

export async function verifyOtpChallenge(token: string | undefined): Promise<OtpChallenge | null> {
  const challenge = await codec.verify<OtpChallenge>(token);
  if (!challenge || isExpired(challenge.exp)) return null;
  if (typeof challenge.phone !== 'string' || typeof challenge.codeHash !== 'string') return null;
  return challenge;
}

export async function signTwoFactorChallenge(
  challenge: Omit<TwoFactorChallenge, 'exp'>,
  /** Kept from the original challenge so a wrong guess cannot extend the window. */
  expiresAt = Math.floor(Date.now() / 1000) + TWO_FACTOR_MAX_AGE,
): Promise<string> {
  return codec.sign({ ...challenge, exp: expiresAt });
}

export async function verifyTwoFactorChallenge(
  token: string | undefined,
): Promise<TwoFactorChallenge | null> {
  const challenge = await codec.verify<TwoFactorChallenge>(token);
  if (!challenge || isExpired(challenge.exp)) return null;
  if (typeof challenge.challenge !== 'string' || challenge.challenge.length === 0) return null;
  if (typeof challenge.attempts !== 'number') return null;
  return challenge;
}

/** SHA-256, hex — used to compare the development password without storing it. */
export async function hashSecret(value: string): Promise<string> {
  return sha256Hex(value);
}
