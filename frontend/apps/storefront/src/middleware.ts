import { NextResponse, type NextRequest } from 'next/server';
import { isSameOriginRequest } from '@bojan/config/origin';
import { SESSION_COOKIE, verifySession } from '@/lib/auth/session';

/**
 * Route protection, plus the storefront's half of maintenance mode.
 *
 * Everything under the customer's own area needs a valid session. The check is
 * a signature verification, not a "is the cookie present" test, so forging the
 * cookie by hand does not get past it.
 */

const PROTECTED = [
  '/account',
  '/checkout',
  '/business/requests',
  '/business/quotes',
  '/login/complete-profile',
];

function isProtected(pathname: string): boolean {
  return PROTECTED.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}

const MAINTENANCE_BYPASS_COOKIE = 'mnt_bypass';

/**
 * What `Retry-After` tells a crawler while the shop is closed.
 *
 * Ten minutes: long enough that a crawler backs off rather than hammering,
 * short enough that it returns soon after the switch goes off.
 */
const MAINTENANCE_RETRY_AFTER_SECONDS = 600;

// The maintenance matcher now runs on nearly every request, so this answer is
// cached in the isolate for a few seconds rather than fetched fresh each
// time — otherwise every navigation on a healthy site would pay for a round
// trip to the API just to hear "no". Five seconds is short enough that
// turning the switch on in the panel still takes effect almost immediately.
const MAINTENANCE_CACHE_MS = 5_000;
let maintenanceCache: { value: boolean; expiresAt: number } | null = null;

/**
 * The call currently out, so that a hundred requests arriving on a cold cache
 * ask the API once between them and not a hundred times.
 *
 * The cache is only consulted before the fetch and written after it, and
 * `await` is a yield: every request that arrives while the first is in flight
 * used to find the same expired entry and start its own call. That is worst
 * exactly when it hurts most — a burst of traffic, or an API slow enough that
 * the window stays open. Sharing the promise makes the second caller wait for
 * the first's answer instead of asking again.
 *
 * This is not about thread safety. Both runtimes this can execute in are
 * single-threaded, so neither the cache nor this handle can be torn; the
 * overlap is between awaits, which is a hazard the Edge runtime has too.
 */
let maintenanceInFlight: Promise<boolean> | null = null;

/** Never rejects: an unreachable API is an answer of "not in maintenance". */
async function readMaintenanceMode(base: string): Promise<boolean> {
  try {
    const response = await fetch(`${base}/store/status`, { cache: 'no-store' });
    return (
      response.ok &&
      ((await response.json()) as { maintenanceMode?: boolean }).maintenanceMode === true
    );
  } catch {
    return false;
  }
}

/**
 * Reads the switch admin sets on screen 150. Fails open on any error — a
 * status check that cannot reach the API must never be what takes the
 * storefront down for every visitor.
 */
async function isMaintenanceModeEnabled(): Promise<boolean> {
  if (maintenanceCache && maintenanceCache.expiresAt > Date.now()) {
    return maintenanceCache.value;
  }

  const base = process.env.API_BASE_URL;
  if (!base) return false;

  // A failed read is cached like any other, and deliberately: it means an API
  // that is down is asked again in five seconds rather than on every request
  // an unreachable backend receives.
  maintenanceInFlight ??= readMaintenanceMode(base).then((value) => {
    maintenanceCache = { value, expiresAt: Date.now() + MAINTENANCE_CACHE_MS };
    maintenanceInFlight = null;
    return value;
  });

  return maintenanceInFlight;
}

/** Methods that change something, and so must prove where they came from. */
const UNSAFE_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

export async function middleware(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  /*
    Before anything else, including the maintenance switch: a write that has not
    shown where it came from is refused whatever the shop's state.

    The panel's cache invalidation is the one write with no origin to show. It
    is a server-to-server call from the admin container — no browser, no
    `Origin`, no `Sec-Fetch-Site` — so the guard refused it, `revalidateStorefront`
    swallowed the 403 as designed, and the tag was never dropped. An article
    published in the panel then stayed off the magazine for the full hour of its
    revalidate window, which is exactly the fault it was reported as.

    It is exempted by route rather than by loosening the check: `/api/revalidate`
    authenticates with API_KEY, which never leaves the compose network, and it
    verifies that key itself before touching a tag. Nothing else is let through.
  */
  const isInternalRevalidate = pathname === '/api/revalidate';

  if (UNSAFE_METHODS.has(request.method) && !isInternalRevalidate && !isSameOriginRequest(request)) {
    return NextResponse.json({ error: 'درخواست معتبر نیست.' }, { status: 403 });
  }

  // Grants a bypass and drops the secret from the visible URL in the same
  // step, rather than leaving it sitting in the address bar or browser history.
  const bypassSecret = process.env.MAINTENANCE_BYPASS_SECRET;
  const bypassParam = request.nextUrl.searchParams.get('maintenance_bypass');
  if (bypassSecret && bypassParam === bypassSecret) {
    const url = request.nextUrl.clone();
    url.searchParams.delete('maintenance_bypass');
    const response = NextResponse.redirect(url);
    response.cookies.set(MAINTENANCE_BYPASS_COOKIE, '1', {
      httpOnly: true,
      sameSite: 'lax',
      // Was the one cookie in either app that skipped this. It is set from a
      // secret in the query string, so sending it in the clear on a downgrade
      // hands over the preview.
      secure: process.env.NODE_ENV === 'production',
      path: '/',
      maxAge: 60 * 60 * 12,
    });
    return response;
  }

  const hasBypass = request.cookies.get(MAINTENANCE_BYPASS_COOKIE)?.value === '1';

  if (pathname !== '/maintenance' && !hasBypass && (await isMaintenanceModeEnabled())) {
    const url = request.nextUrl.clone();
    url.pathname = '/maintenance';
    url.search = '';

    // 503, not the 200 a bare rewrite produces. The maintenance page carries
    // `robots: noindex`, and "200 with noindex" on a URL that is already
    // indexed is the instruction to *remove* it — so a maintenance window long
    // enough for a crawl told Google to drop the homepage, every category and
    // every product page. 503 is the response that means "temporarily away,
    // change nothing", and `Retry-After` says for how long.
    return NextResponse.rewrite(url, {
      status: 503,
      headers: { 'Retry-After': String(MAINTENANCE_RETRY_AFTER_SECONDS) },
    });
  }

  const session = await verifySession(request.cookies.get(SESSION_COOKIE)?.value);

  if (isProtected(pathname) && !session) {
    const url = request.nextUrl.clone();
    url.pathname = '/login';
    url.search = '';
    // Path + query only; `safeNext` on the way back refuses anything else.
    url.searchParams.set('next', `${pathname}${search}`);

    const response = NextResponse.redirect(url);
    // A stale or tampered cookie is not worth carrying forward.
    if (request.cookies.has(SESSION_COOKIE)) response.cookies.delete(SESSION_COOKIE);
    return response;
  }

  // Signed in and asking for the sign-in or register screen — send them home
  // instead. `/login/complete-profile` is exempt: it is only reachable *with*
  // a session, and registering lands on it.
  //
  // The password-recovery pair is deliberately not here. Someone signed in on
  // one device who followed a reset link from their inbox is doing something
  // legitimate, and bouncing them would leave the link with nowhere to go.
  if (session && (pathname === '/login' || pathname === '/register')) {
    const url = request.nextUrl.clone();
    url.pathname = '/account';
    url.search = '';
    return NextResponse.redirect(url);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    // Everything except Next's own internals, static assets and files with an
    // extension (images, fonts, manifest) — those must reach the page/CDN
    // untouched even while the storefront is in maintenance mode.
    '/((?!_next/static|_next/image|favicon\\.ico|.*\\..*).*)',
  ],
};
