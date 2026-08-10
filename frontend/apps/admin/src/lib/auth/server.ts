import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import { safeNextPath } from '@bojan/config/safe-next';
import { SESSION_COOKIE, verifySession, type AdminRole, type AdminSession } from './session';

/**
 * Session helpers for admin server components and route handlers.
 *
 * The middleware decides whether a request gets in at all; these decide what
 * the operator inside is allowed to see.
 */

export async function getAdminSession(): Promise<AdminSession | null> {
  const store = await cookies();
  return verifySession(store.get(SESSION_COOKIE)?.value);
}

export async function requireAdminSession(): Promise<AdminSession> {
  const session = await getAdminSession();
  if (!session) redirect('/login');
  return session;
}

/**
 * Screens 145-147 and the settings group — owner-only areas.
 *
 * Sends anyone else to screen 158 (عدم دسترسی) rather than to sign-in: they
 * are signed in correctly, they just do not hold this permission.
 */
export async function requireRole(...roles: AdminRole[]): Promise<AdminSession> {
  const session = await requireAdminSession();
  if (!roles.includes(session.role)) redirect('/forbidden');
  return session;
}

/** Re-exported so server code has one import for everything session-related. */
export { safeNextPath };
