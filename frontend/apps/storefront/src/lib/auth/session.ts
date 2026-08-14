/**
 * The shopper's signed session cookie.
 *
 * What is in it, how long it lasts, and which secret signs it. The envelope —
 * base64url payload, dot, HMAC-SHA256 — is `@bojan/config/signed-cookie`,
 * shared with the panel so that a fix to the signing applies to both sessions
 * rather than to whichever one someone remembered.
 *
 * The API issues the bearer token at sign-in and this cookie carries it; the
 * cookie itself is minted here because the middleware has to be able to read a
 * session without a round trip, on every request, in the Edge runtime.
 */

import { createSignedCookieCodec, isExpired, sha256Hex } from '@bojan/config/signed-cookie';

export const SESSION_COOKIE = 'bojan_session';
export const OTP_COOKIE = 'bojan_otp';

/** Thirty days, matching the "remember me" window the design implies. */
export const SESSION_MAX_AGE = 60 * 60 * 24 * 30;

/** An OTP challenge is short-lived; the design's resend timer is two minutes. */
export const OTP_MAX_AGE = 60 * 5;

/** Wrong codes allowed before the challenge is burned. */
export const OTP_MAX_ATTEMPTS = 5;

export interface SessionPayload {
  /** Customer id. */
  sub: string;
  phone: string;
  name?: string;
  /**
   * Bearer token the API issued at sign-in, forwarded on every call the data
   * layer makes on this customer's behalf. Optional because a session minted
   * before the backend existed has none — those fall back to the id, which the
   * API only trusts alongside the server's own shared secret.
   */
  token?: string;
  /**
   * The account's security stamp, forwarded to the API as `X-Customer-Stamp`.
   *
   * A rotated stamp — a password reset — makes every cookie signed before it
   * stop authenticating, which is what a signed, self-contained cookie cannot
   * otherwise do. Optional only so the type describes a cookie minted before
   * this existed; the API refuses a customer request without one, so those
   * sessions end at the next call rather than lingering unrevokable.
   */
  stamp?: string;
  /** Expiry, unix seconds. */
  exp: number;
}

export interface OtpChallenge {
  phone: string;
  /** Hex SHA-256 of the code — never the code itself. */
  codeHash: string;
  attempts: number;
  exp: number;
}

/**
 * Cookie options shared by both cookies. `httpOnly` keeps them away from
 * scripts, `sameSite: lax` survives the top-level navigation back from the
 * payment gateway while still blocking cross-site POSTs.
 */
export const cookieOptions = {
  httpOnly: true,
  sameSite: 'lax',
  secure: process.env.NODE_ENV === 'production',
  path: '/',
} as const;

const DEV_SECRET = 'bojan-development-secret-do-not-use-in-production';

function secretBytes(): Uint8Array {
  const configured = process.env.AUTH_SECRET;

  if (!configured || configured.length < 32) {
    if (process.env.NODE_ENV === 'production') {
      throw new Error(
        'AUTH_SECRET is missing or shorter than 32 characters. Session tokens cannot be signed.',
      );
    }
    return new TextEncoder().encode(DEV_SECRET);
  }

  return new TextEncoder().encode(configured);
}

const codec = createSignedCookieCodec(secretBytes);

export async function signSession(payload: Omit<SessionPayload, 'exp'>): Promise<string> {
  return codec.sign({ ...payload, exp: Math.floor(Date.now() / 1000) + SESSION_MAX_AGE });
}

export async function verifySession(token: string | undefined): Promise<SessionPayload | null> {
  const payload = await codec.verify<SessionPayload>(token);
  if (!payload || isExpired(payload.exp)) return null;
  if (typeof payload.sub !== 'string' || typeof payload.phone !== 'string') return null;
  return payload;
}

export async function signOtpChallenge(
  challenge: Omit<OtpChallenge, 'exp'>,
): Promise<string> {
  return codec.sign({ ...challenge, exp: Math.floor(Date.now() / 1000) + OTP_MAX_AGE });
}

export async function verifyOtpChallenge(token: string | undefined): Promise<OtpChallenge | null> {
  const challenge = await codec.verify<OtpChallenge>(token);
  if (!challenge || isExpired(challenge.exp)) return null;
  if (typeof challenge.phone !== 'string' || typeof challenge.codeHash !== 'string') return null;
  return challenge;
}

/** SHA-256, hex. Used so a stolen challenge cookie does not reveal the code. */
export async function hashCode(code: string): Promise<string> {
  return sha256Hex(code);
}
