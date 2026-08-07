/**
 * Who the chat widget is, as something the browser holds but cannot read.
 *
 * The id used to be a `crypto.randomUUID()` in `localStorage`, sent back in the
 * URL: `/api/chat/<that guid>`. Two things followed from that. Any script on
 * the page could read it — an extension, an injected tag, anything an XSS ever
 * reaches — and it never changed, so one leak exposed every conversation that
 * visitor would ever have. And because the id *was* the credential, anyone who
 * came by one could replay it and read the thread; the operator's own screen
 * shows these ids.
 *
 * So it moves into an http-only cookie and gets signed. Http-only takes it away
 * from page scripts. The signature is what a bare random id could not give: an
 * id learned from somewhere else cannot be presented, because presenting it
 * needs this server's key.
 */

/** A visitor is remembered for a month, not forever. */
export const VISITOR_COOKIE = 'bojan_chat_visitor';
export const VISITOR_MAX_AGE = 60 * 60 * 24 * 30;

export const visitorCookieOptions = {
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
      throw new Error('AUTH_SECRET is missing or shorter than 32 characters. Chat ids cannot be signed.');
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

/** Mints a new visitor and returns the cookie value that names them. */
export async function signVisitorId(visitorId: string): Promise<string> {
  const signature = await crypto.subtle.sign(
    'HMAC',
    await hmacKey(),
    new TextEncoder().encode(visitorId) as unknown as ArrayBuffer,
  );
  return `${visitorId}.${bytesToBase64Url(new Uint8Array(signature))}`;
}

/**
 * The visitor a cookie names, or null when it is absent, forged, or not a
 * shape the API will accept.
 */
export async function verifyVisitorId(token: string | undefined): Promise<string | null> {
  if (!token) return null;

  const separator = token.lastIndexOf('.');
  if (separator <= 0) return null;

  const visitorId = token.slice(0, separator);

  // The API routes these by GUID. Checking the shape here keeps a malformed
  // value from travelling any further than the cookie it arrived in.
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(visitorId)) return null;

  try {
    const expected = new Uint8Array(
      await crypto.subtle.sign(
        'HMAC',
        await hmacKey(),
        new TextEncoder().encode(visitorId) as unknown as ArrayBuffer,
      ),
    );
    if (!timingSafeEqual(expected, base64UrlToBytes(token.slice(separator + 1)))) return null;

    return visitorId;
  } catch {
    return null;
  }
}
