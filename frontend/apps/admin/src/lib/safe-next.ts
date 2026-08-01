/**
 * Open-redirect guard for the sign-in screen's `?next=`.
 *
 * Free of server-only imports so the sign-in form and the middleware can both
 * use it. Anything that is not a same-origin absolute path falls back.
 */
export function safeNextPath(value: string | null | undefined, fallback: string): string {
  if (!value || !value.startsWith('/')) return fallback;
  if (value.startsWith('//') || value.startsWith('/\\')) return fallback;
  return value;
}
