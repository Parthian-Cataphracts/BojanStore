import { NextResponse, type NextRequest } from 'next/server';
import { isSameOriginRequest } from '@bojan/config/origin';
import { SESSION_COOKIE, verifySession } from '@/lib/auth/session';
import { canOpenPath } from '@/lib/permissions';

/**
 * The panel is closed by default.
 *
 * Everything except the sign-in screen and its own auth endpoints requires a
 * valid session, so a new admin screen is protected the moment it is added —
 * nobody has to remember to list it. The check verifies the token's signature,
 * so a hand-written cookie does not open the panel.
 */

// The sign-in screen, its OTP variant, and the endpoints they call. Everything
// under `/api/admin-auth` is reachable signed-out by design — signing in and
// signing out both have to work without a session.
const PUBLIC_PATHS = ['/login', '/api/admin-auth'];

function isPublic(pathname: string): boolean {
  return PUBLIC_PATHS.some((path) => pathname === path || pathname.startsWith(`${path}/`));
}

/**
 * The only things reachable while a session is flagged
 * `mustChangePassword` — the screen that changes it, the endpoint that screen
 * posts to, and the auth routes so signing out is always possible.
 *
 * An operator appointed from screen 145 starts with a password their owner
 * typed and then read out to them, so for as long as they have not replaced it,
 * the credential is known to two people and only one of them owns it. The flag
 * is cleared by `POST /me/password`, which is the one route that asks for the
 * current password before setting the next.
 */
const PASSWORD_CHANGE_PATHS = ['/settings/password', '/api/admin/password', '/api/admin-auth'];

/** Methods that change something, and so must prove where they came from. */
const UNSAFE_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

export async function middleware(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  // Here rather than in each route handler for the same reason the session
  // check is: a panel this size grows endpoints, and a defence that has to be
  // remembered is one that eventually is not.
  if (UNSAFE_METHODS.has(request.method) && !isSameOriginRequest(request)) {
    return NextResponse.json({ error: 'درخواست معتبر نیست.' }, { status: 403 });
  }

  const session = await verifySession(request.cookies.get(SESSION_COOKIE)?.value);

  if (session) {
    // Already signed in — the sign-in screen has nothing to offer.
    if (pathname === '/login') {
      const url = request.nextUrl.clone();
      url.pathname = '/';
      url.search = '';
      return NextResponse.redirect(url);
    }

    // Here rather than on each page, for the reason the session check itself
    // is: a requirement that every new screen has to remember is one that a
    // new screen eventually forgets, and the screens this protects are the
    // whole panel.
    if (
      session.mustChangePassword &&
      !PASSWORD_CHANGE_PATHS.some((path) => pathname === path || pathname.startsWith(`${path}/`))
    ) {
      const url = request.nextUrl.clone();
      url.pathname = '/settings/password';
      url.search = '';
      return NextResponse.redirect(url);
    }

    /*
      A screen this operator was not granted.

      The menu already leaves it out, and that is not enough on its own: the
      address is still typeable, still bookmarked from before the grant
      narrowed, and still reachable from a link a colleague pasted. Here rather
      than on each page for the reason the session check is — a requirement
      every new screen has to remember is one a new screen eventually forgets.

      Only the panel's own routes: `/api/admin/*` proxies to the API, which
      applies its own permission filter to what it forwards, and turning one of
      those into an HTML redirect would answer a fetch with a login page.
    */
    if (
      !pathname.startsWith('/api/') &&
      !canOpenPath(session.sections?.length ? session.sections : null, pathname)
    ) {
      const url = request.nextUrl.clone();
      url.pathname = '/forbidden';
      url.search = '';
      return NextResponse.redirect(url);
    }

    return NextResponse.next();
  }

  if (isPublic(pathname)) return NextResponse.next();

  const url = request.nextUrl.clone();
  url.pathname = '/login';
  url.search = '';
  url.searchParams.set('next', `${pathname}${search}`);

  const response = NextResponse.redirect(url);
  if (request.cookies.has(SESSION_COOKIE)) response.cookies.delete(SESSION_COOKIE);
  return response;
}

export const config = {
  // Everything except Next's own assets and the files served straight out of
  // `public/`. Listing what is *not* protected is the only safe direction for a
  // panel this size — but the list has to include `public/`, which the matcher
  // does not exclude on its own.
  //
  // The icon font lives there, and the sign-in screen is the one page that
  // needs it while signed out. Without the `.*\..*` clause its request was
  // redirected to `/login`, so the browser received an HTML document where a
  // woff2 was expected, the font failed to load, and every icon on the sign-in
  // form rendered as its own name — "person", "lock", "visibility", "login".
  // The storefront's matcher has always carried this clause; the panel's did
  // not.
  matcher: ['/((?!_next/static|_next/image|favicon\\.ico|robots\\.txt|.*\\..*).*)'],
};
