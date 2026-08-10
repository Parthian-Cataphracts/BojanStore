import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import { routes } from '@/lib/routes';
import { safeNextPath } from '@bojan/config/safe-next';
import { SESSION_COOKIE, verifySession, type SessionPayload } from './session';

/**
 * Session helpers for server components and route handlers.
 *
 * The middleware already blocks unauthenticated requests to protected routes;
 * these exist so a page can read *who* is signed in, and so a page that is only
 * conditionally protected can guard itself.
 */

export async function getSession(): Promise<SessionPayload | null> {
  const store = await cookies();
  return verifySession(store.get(SESSION_COOKIE)?.value);
}

/**
 * Session or bounce to sign-in. `next` is a path only — never a full URL — so
 * it cannot be turned into an open redirect.
 */
export async function requireSession(next?: string): Promise<SessionPayload> {
  const session = await getSession();
  if (session) return session;

  redirect(next ? `${routes.login}?next=${encodeURIComponent(next)}` : routes.login);
}

/** Re-exported so server code has one import for everything session-related. */
export { safeNextPath };
