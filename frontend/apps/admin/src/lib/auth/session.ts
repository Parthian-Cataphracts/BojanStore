/**
 * Signed admin session tokens.
 *
 * Same construction as the storefront's, with a different cookie name and a
 * different payload: the panel needs the operator's role to decide what they
 * may see, and a much shorter lifetime than a shopper's session.
 *
 * Web Crypto throughout so this runs unchanged in the Edge runtime
 * (middleware) and the Node runtime (route handlers, server components).
 */

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
  /** Expiry, unix seconds. */
  exp: number;
}

export const cookieOptions = {
  httpOnly: true,
  // Stricter than the storefront: the panel has no payment-gateway round trip
  // to come back from, so there is no reason to accept cross-site navigation.
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

function bytesToBase64Url(bytes: Uint8Array): string {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function base64UrlToBytes(value: string): Uint8Array {
  const padded = value.replace(/-/g, '+').replace(/_/g, '/');
  const binary = atob(padded + '='.repeat((4 - (padded.length % 4)) % 4));
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) bytes[index] = binary.charCodeAt(index);
  return bytes;
}

async function hmacKey(): Promise<CryptoKey> {
  return crypto.subtle.importKey(
    'raw',
    secretBytes() as unknown as ArrayBuffer,
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign', 'verify'],
  );
}

function timingSafeEqual(a: Uint8Array, b: Uint8Array): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let index = 0; index < a.length; index += 1) diff |= (a[index] ?? 0) ^ (b[index] ?? 0);
  return diff === 0;
}

export async function signSession(payload: Omit<AdminSession, 'exp'>): Promise<string> {
  const body = bytesToBase64Url(
    new TextEncoder().encode(
      JSON.stringify({ ...payload, exp: Math.floor(Date.now() / 1000) + SESSION_MAX_AGE }),
    ),
  );
  const signature = await crypto.subtle.sign(
    'HMAC',
    await hmacKey(),
    new TextEncoder().encode(body) as unknown as ArrayBuffer,
  );
  return `${body}.${bytesToBase64Url(new Uint8Array(signature))}`;
}

export async function verifySession(token: string | undefined): Promise<AdminSession | null> {
  if (!token) return null;

  const separator = token.lastIndexOf('.');
  if (separator <= 0) return null;

  const body = token.slice(0, separator);

  try {
    const expected = new Uint8Array(
      await crypto.subtle.sign(
        'HMAC',
        await hmacKey(),
        new TextEncoder().encode(body) as unknown as ArrayBuffer,
      ),
    );
    if (!timingSafeEqual(expected, base64UrlToBytes(token.slice(separator + 1)))) return null;

    const payload = JSON.parse(new TextDecoder().decode(base64UrlToBytes(body))) as AdminSession;
    if (typeof payload.exp !== 'number' || payload.exp * 1000 <= Date.now()) return null;
    if (typeof payload.sub !== 'string' || typeof payload.role !== 'string') return null;
    // A cookie minted before the stamp existed is refused rather than carried:
    // it is exactly the credential revocation cannot reach.
    if (typeof payload.stamp !== 'string' || payload.stamp.length === 0) return null;

    return payload;
  } catch {
    return null;
  }
}

export async function signOtpChallenge(
  challenge: Omit<OtpChallenge, 'exp'>,
): Promise<string> {
  const body = bytesToBase64Url(
    new TextEncoder().encode(
      JSON.stringify({ ...challenge, exp: Math.floor(Date.now() / 1000) + OTP_MAX_AGE }),
    ),
  );
  const signature = await crypto.subtle.sign(
    'HMAC',
    await hmacKey(),
    new TextEncoder().encode(body) as unknown as ArrayBuffer,
  );
  return `${body}.${bytesToBase64Url(new Uint8Array(signature))}`;
}

export async function verifyOtpChallenge(token: string | undefined): Promise<OtpChallenge | null> {
  if (!token) return null;

  const separator = token.lastIndexOf('.');
  if (separator <= 0) return null;

  const body = token.slice(0, separator);

  try {
    const expected = new Uint8Array(
      await crypto.subtle.sign(
        'HMAC',
        await hmacKey(),
        new TextEncoder().encode(body) as unknown as ArrayBuffer,
      ),
    );
    if (!timingSafeEqual(expected, base64UrlToBytes(token.slice(separator + 1)))) return null;

    const challenge = JSON.parse(new TextDecoder().decode(base64UrlToBytes(body))) as OtpChallenge;
    if (typeof challenge.exp !== 'number' || challenge.exp * 1000 <= Date.now()) return null;
    if (typeof challenge.phone !== 'string' || typeof challenge.codeHash !== 'string') return null;

    return challenge;
  } catch {
    return null;
  }
}

export async function signTwoFactorChallenge(
  challenge: Omit<TwoFactorChallenge, 'exp'>,
  /** Kept from the original challenge so a wrong guess cannot extend the window. */
  expiresAt = Math.floor(Date.now() / 1000) + TWO_FACTOR_MAX_AGE,
): Promise<string> {
  const body = bytesToBase64Url(
    new TextEncoder().encode(JSON.stringify({ ...challenge, exp: expiresAt })),
  );
  const signature = await crypto.subtle.sign(
    'HMAC',
    await hmacKey(),
    new TextEncoder().encode(body) as unknown as ArrayBuffer,
  );
  return `${body}.${bytesToBase64Url(new Uint8Array(signature))}`;
}

export async function verifyTwoFactorChallenge(
  token: string | undefined,
): Promise<TwoFactorChallenge | null> {
  if (!token) return null;

  const separator = token.lastIndexOf('.');
  if (separator <= 0) return null;

  const body = token.slice(0, separator);

  try {
    const expected = new Uint8Array(
      await crypto.subtle.sign(
        'HMAC',
        await hmacKey(),
        new TextEncoder().encode(body) as unknown as ArrayBuffer,
      ),
    );
    if (!timingSafeEqual(expected, base64UrlToBytes(token.slice(separator + 1)))) return null;

    const challenge = JSON.parse(
      new TextDecoder().decode(base64UrlToBytes(body)),
    ) as TwoFactorChallenge;
    if (typeof challenge.exp !== 'number' || challenge.exp * 1000 <= Date.now()) return null;
    if (typeof challenge.challenge !== 'string' || challenge.challenge.length === 0) return null;
    if (typeof challenge.attempts !== 'number') return null;

    return challenge;
  } catch {
    return null;
  }
}

/** SHA-256, hex — used to compare the development password without storing it. */
export async function hashSecret(value: string): Promise<string> {
  const digest = await crypto.subtle.digest(
    'SHA-256',
    new TextEncoder().encode(value) as unknown as ArrayBuffer,
  );
  return [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, '0')).join('');
}
