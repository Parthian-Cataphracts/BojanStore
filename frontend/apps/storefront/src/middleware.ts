import { NextResponse, type NextRequest } from 'next/server';
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

// The maintenance matcher now runs on nearly every request, so this answer is
// cached in the isolate for a few seconds rather than fetched fresh each
// time — otherwise every navigation on a healthy site would pay for a round
// trip to the API just to hear "no". Five seconds is short enough that
// turning the switch on in the panel still takes effect almost immediately.
const MAINTENANCE_CACHE_MS = 5_000;
let maintenanceCache: { value: boolean; expiresAt: number } | null = null;

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

  try {
    const response = await fetch(`${base}/store/status`, { cache: 'no-store' });
    const enabled = response.ok && ((await response.json()) as { maintenanceMode?: boolean }).maintenanceMode === true;
    maintenanceCache = { value: enabled, expiresAt: Date.now() + MAINTENANCE_CACHE_MS };
    return enabled;
  } catch {
    return false;
  }
}

export async function middleware(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

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
      maxAge: 60 * 60 * 12,
    });
    return response;
  }

  const hasBypass = request.cookies.get(MAINTENANCE_BYPASS_COOKIE)?.value === '1';

  if (pathname !== '/maintenance' && !hasBypass && (await isMaintenanceModeEnabled())) {
    const url = request.nextUrl.clone();
    url.pathname = '/maintenance';
    url.search = '';
    return NextResponse.rewrite(url);
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
