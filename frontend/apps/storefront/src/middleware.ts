import { NextResponse, type NextRequest } from 'next/server';
import { SESSION_COOKIE, verifySession } from '@/lib/auth/session';

/**
 * Route protection.
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

export async function middleware(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
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
    '/account/:path*',
    '/checkout/:path*',
    '/business/requests/:path*',
    '/business/quotes/:path*',
    '/login',
    '/login/:path*',
    '/register',
  ],
};
