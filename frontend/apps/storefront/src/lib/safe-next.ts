/**
 * Open-redirect guard for the `?next=` parameter.
 *
 * Kept free of server-only imports so the sign-in form and the middleware can
 * both use it. Anything that is not a same-origin absolute path falls back:
 *
 * - `https://evil.example` — has a scheme
 * - `//evil.example` — protocol-relative
 * - `/\evil.example` — browsers normalise the backslash to a second slash
 * - `/\t/evil.example` — see below
 *
 * The last one is why this parses rather than pattern-matches. Tab, newline and
 * carriage return are *removed* from a URL before it is parsed, per the WHATWG
 * URL standard, so a browser reads `/<TAB>/evil.example` as `//evil.example`
 * and leaves the origin. A prefix test on the raw string sees a lone leading
 * slash and waves it through. Stripping those three characters first, then
 * resolving the result against a throwaway origin and insisting the origin
 * survived, checks the value the browser will actually act on.
 */

/** Removed from URLs by the parser itself — so removed here before deciding. */
const URL_STRIPPED = /[\t\n\r]/g;

const PLACEHOLDER_ORIGIN = 'https://bojan.invalid';

export function safeNextPath(value: string | null | undefined, fallback: string): string {
  if (!value) return fallback;

  const candidate = value.replace(URL_STRIPPED, '');

  // A backslash is not stripped but *is* normalised to a forward slash by the
  // parser, which turns `/\evil.example` into a protocol-relative URL.
  if (!candidate.startsWith('/') || candidate.startsWith('//') || candidate.startsWith('/\\')) {
    return fallback;
  }

  let resolved: URL;
  try {
    resolved = new URL(candidate, PLACEHOLDER_ORIGIN);
  } catch {
    return fallback;
  }

  // Anything that reached out to another host, or carried a scheme of its own,
  // has left the placeholder origin behind.
  if (resolved.origin !== PLACEHOLDER_ORIGIN) return fallback;

  // Returned re-serialised rather than as it arrived: the caller should redirect
  // to the thing that was checked, not to the string it was checked from.
  return `${resolved.pathname}${resolved.search}${resolved.hash}`;
}
