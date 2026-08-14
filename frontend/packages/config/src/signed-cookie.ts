/**
 * The envelope both apps mint their session cookies in.
 *
 * A payload as base64url JSON, a dot, and an HMAC-SHA256 of that text: the
 * cookie is tamper-evident without a server-side session table, and a forged
 * one fails verification rather than being trusted.
 *
 * It lives here because it was written twice — once in each app — and the two
 * copies had already drifted: the storefront factored its signing into one
 * `sign`/`verify` pair, while the panel inlined the same fourteen lines into
 * four separate functions. That is the shape a security fix gets applied to
 * three places out of four. Nothing about the two sessions is actually
 * different at this level; what differs is the payload, the cookie name, the
 * lifetime and the secret, and all four of those stay in the app that owns
 * them.
 *
 * Web Crypto only, so this runs unchanged in the Edge runtime (middleware) and
 * the Node runtime (route handlers, server components).
 */

/**
 * Where the signing key comes from, asked for at every use rather than read
 * once.
 *
 * Each app resolves its own `AUTH_SECRET` — deliberately different secrets, so
 * a customer cookie can never be replayed as an operator one — and each throws
 * its own error when a production host has not set it. Resolving per call keeps
 * that check on the request path instead of at module load, where in Next it
 * would fire during a build.
 */
export type SecretResolver = () => Uint8Array;

export interface SignedCookieCodec {
  /** Signs a payload as it stands. Expiry is the caller's to put in it. */
  sign(payload: unknown): Promise<string>;
  /**
   * The payload, or null for anything that is not a token this key signed.
   *
   * Shape and expiry are not checked here: only the caller knows what its own
   * payload should look like, and a codec that guessed would either be too
   * permissive or reject a field it had never heard of.
   */
  verify<T>(token: string | undefined): Promise<T | null>;
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

/**
 * Compares two signatures without leaking which byte differed.
 *
 * The early return on length is not a timing leak: both operands are
 * HMAC-SHA256 outputs — thirty-two bytes, always — so the lengths match on
 * every real comparison, and a mismatched one reveals only that the caller sent
 * something which is not a SHA-256 signature, a public fact about the format.
 * The loop is what matters: it visits every byte rather than stopping at the
 * first difference, so the time says nothing about how much of a forged
 * signature was right.
 */
function timingSafeEqual(a: Uint8Array, b: Uint8Array): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let index = 0; index < a.length; index += 1) diff |= (a[index] ?? 0) ^ (b[index] ?? 0);
  return diff === 0;
}

export function createSignedCookieCodec(resolveSecret: SecretResolver): SignedCookieCodec {
  async function hmacKey(): Promise<CryptoKey> {
    return crypto.subtle.importKey(
      'raw',
      resolveSecret() as unknown as ArrayBuffer,
      { name: 'HMAC', hash: 'SHA-256' },
      false,
      ['sign', 'verify'],
    );
  }

  async function signature(body: string): Promise<Uint8Array> {
    return new Uint8Array(
      await crypto.subtle.sign(
        'HMAC',
        await hmacKey(),
        new TextEncoder().encode(body) as unknown as ArrayBuffer,
      ),
    );
  }

  return {
    async sign(payload) {
      const body = bytesToBase64Url(new TextEncoder().encode(JSON.stringify(payload)));
      return `${body}.${bytesToBase64Url(await signature(body))}`;
    },

    async verify<T>(token: string | undefined) {
      if (!token) return null;

      // From the last dot, not the first: base64url has no dot in its alphabet,
      // so the body cannot contain one — but reading from the end is what makes
      // that true of a payload someone else appended to as well.
      const separator = token.lastIndexOf('.');
      if (separator <= 0) return null;

      const body = token.slice(0, separator);

      try {
        if (!timingSafeEqual(await signature(body), base64UrlToBytes(token.slice(separator + 1)))) {
          return null;
        }

        return JSON.parse(new TextDecoder().decode(base64UrlToBytes(body))) as T;
      } catch {
        // Malformed base64 or JSON — treated exactly like a bad signature,
        // because to a caller it is one.
        return null;
      }
    },
  };
}

/** Unix seconds, in the past or not a number at all. */
export function isExpired(exp: unknown): boolean {
  return typeof exp !== 'number' || exp * 1000 <= Date.now();
}

/**
 * SHA-256, hex.
 *
 * Used where a value has to be compared later but must not be stored — a
 * one-time code parked in a cookie, most of all, so that stealing the cookie
 * does not hand over the code it is guarding.
 */
export async function sha256Hex(value: string): Promise<string> {
  const digest = await crypto.subtle.digest(
    'SHA-256',
    new TextEncoder().encode(value) as unknown as ArrayBuffer,
  );
  return [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, '0')).join('');
}
