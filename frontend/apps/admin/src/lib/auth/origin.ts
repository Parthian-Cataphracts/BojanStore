/**
 * Cross-site request forgery, refused at the door.
 *
 * The session cookie is `SameSite=Strict` here and `Lax` on the storefront,
 * which already keeps it off a cross-site POST in any browser that honours it.
 * That was the whole defence, and it is one attribute away from nothing: a
 * relaxation made for some future integration, a client that treats an unknown
 * value as `None`, or the payment gateway's return trip needing `Lax` — none of
 * which should be the difference between an operator's session being safe and
 * being spendable by any page they happen to open.
 *
 * So the origin is checked as well, and independently. `Sec-Fetch-Site` is the
 * browser's own statement about where the request came from and cannot be set
 * by script; `Origin` is the fallback for the handful of clients that omit it.
 * A request presenting neither is not a browser form post, and these routes
 * serve nothing else.
 */

/**
 * Hosts allowed to originate a write beyond this app's own.
 *
 * Empty in every normal deployment. It exists because the alternative to a
 * configured exception is an unconfigured one — someone loosening the check
 * itself the first time a legitimate second origin appears.
 */
function allowedOrigins(): string[] {
  return (process.env.TRUSTED_ORIGINS ?? '')
    .split(',')
    .map((entry) => entry.trim())
    .filter(Boolean);
}

/** The host this request was actually addressed to, behind the proxy. */
function requestHost(request: Request): string | null {
  const forwarded = request.headers.get('x-forwarded-host');
  if (forwarded) return forwarded.split(',')[0]!.trim().toLowerCase();

  const host = request.headers.get('host');
  return host ? host.trim().toLowerCase() : null;
}

export function isSameOriginRequest(request: Request): boolean {
  const site = request.headers.get('sec-fetch-site');

  if (site) {
    // `none` is a direct navigation — typing the URL, a bookmark. It reaches a
    // POST handler only by way of a form the user submitted from nowhere, which
    // is not a shape this app produces, so it is not on the accept list.
    if (site === 'same-origin') return true;

    const origin = request.headers.get('origin');
    return origin !== null && allowedOrigins().includes(origin);
  }

  const origin = request.headers.get('origin');
  if (!origin) return false;

  if (allowedOrigins().includes(origin)) return true;

  const host = requestHost(request);
  if (!host) return false;

  try {
    return new URL(origin).host.toLowerCase() === host;
  } catch {
    return false;
  }
}
