/**
 * Signed session tokens.
 *
 * The .NET backend is not up yet, so the session is minted here rather than
 * received from it. It is a real HMAC-SHA256 token, not a placeholder: the
 * payload is tamper-evident, carries its own expiry, and is verified on every
 * request by the middleware. When the backend starts issuing JWTs the only
 * change is where `signSession` gets its token from — the cookie contract, the
 * middleware and every caller stay as they are.
 *
 * Web Crypto is used throughout so this module runs unchanged in the Edge
 * runtime (middleware) and the Node runtime (route handlers, server components).
 */

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

/** Length-independent comparison — bails on length, then diffs every byte. */
function timingSafeEqual(a: Uint8Array, b: Uint8Array): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let index = 0; index < a.length; index += 1) diff |= (a[index] ?? 0) ^ (b[index] ?? 0);
  return diff === 0;
}

async function sign(payload: unknown): Promise<string> {
  const body = bytesToBase64Url(new TextEncoder().encode(JSON.stringify(payload)));
  const signature = await crypto.subtle.sign(
    'HMAC',
    await hmacKey(),
    new TextEncoder().encode(body) as unknown as ArrayBuffer,
  );
  return `${body}.${bytesToBase64Url(new Uint8Array(signature))}`;
}

async function verify<T>(token: string | undefined): Promise<T | null> {
  if (!token) return null;

  const separator = token.lastIndexOf('.');
  if (separator <= 0) return null;

  const body = token.slice(0, separator);
  const signature = token.slice(separator + 1);

  try {
    const expected = new Uint8Array(
      await crypto.subtle.sign(
        'HMAC',
        await hmacKey(),
        new TextEncoder().encode(body) as unknown as ArrayBuffer,
      ),
    );
    if (!timingSafeEqual(expected, base64UrlToBytes(signature))) return null;

    return JSON.parse(new TextDecoder().decode(base64UrlToBytes(body))) as T;
  } catch {
    // Malformed base64 or JSON — treat exactly like a bad signature.
    return null;
  }
}

function isExpired(exp: unknown): boolean {
  return typeof exp !== 'number' || exp * 1000 <= Date.now();
}

export async function signSession(payload: Omit<SessionPayload, 'exp'>): Promise<string> {
  return sign({ ...payload, exp: Math.floor(Date.now() / 1000) + SESSION_MAX_AGE });
}

export async function verifySession(token: string | undefined): Promise<SessionPayload | null> {
  const payload = await verify<SessionPayload>(token);
  if (!payload || isExpired(payload.exp)) return null;
  if (typeof payload.sub !== 'string' || typeof payload.phone !== 'string') return null;
  return payload;
}

export async function signOtpChallenge(
  challenge: Omit<OtpChallenge, 'exp'>,
): Promise<string> {
  return sign({ ...challenge, exp: Math.floor(Date.now() / 1000) + OTP_MAX_AGE });
}

export async function verifyOtpChallenge(token: string | undefined): Promise<OtpChallenge | null> {
  const challenge = await verify<OtpChallenge>(token);
  if (!challenge || isExpired(challenge.exp)) return null;
  if (typeof challenge.phone !== 'string' || typeof challenge.codeHash !== 'string') return null;
  return challenge;
}

/** SHA-256, hex. Used so a stolen challenge cookie does not reveal the code. */
export async function hashCode(code: string): Promise<string> {
  const digest = await crypto.subtle.digest(
    'SHA-256',
    new TextEncoder().encode(code) as unknown as ArrayBuffer,
  );
  return [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, '0')).join('');
}
