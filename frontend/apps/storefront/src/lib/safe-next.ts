/**
 * Open-redirect guard for the `?next=` parameter.
 *
 * Kept free of server-only imports so the sign-in form and the middleware can
 * both use it. Anything that is not a same-origin absolute path falls back:
 *
 * - `https://evil.example` — has a scheme
 * - `//evil.example` — protocol-relative
 * - `/\evil.example` — browsers normalise the backslash to a second slash
 */
export function safeNextPath(value: string | null | undefined, fallback: string): string {
  if (!value || !value.startsWith('/')) return fallback;
  if (value.startsWith('//') || value.startsWith('/\\')) return fallback;
  return value;
}
