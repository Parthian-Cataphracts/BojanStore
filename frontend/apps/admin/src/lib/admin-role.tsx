'use client';

import { createContext, useContext, useMemo, type ReactNode } from 'react';
import type { AdminRole } from '@/lib/auth/session';

interface AdminIdentity {
  role: AdminRole | null;
  /**
   * The sections and screens this operator is narrowed to, or `null` for an
   * operator nobody has narrowed — which is everything their role allows, not
   * nothing. See `lib/permissions`.
   */
  grants: readonly string[] | null;
}

const IdentityContext = createContext<AdminIdentity>({ role: null, grants: null });

/**
 * The signed-in operator's role and permissions, for the parts of the panel
 * that have to draw differently depending on them.
 *
 * A context rather than a prop because the one consumer, `AdminNavList`, is
 * rendered twice from two different places — the desktop rail and the mobile
 * drawer — and the drawer hangs off `AdminTopBar`, which every page renders for
 * itself. Threading these down that path would have meant new props on four
 * components that have no use for them, which is the same reason
 * `SidebarStateProvider` exists.
 *
 * `null` role means "not signed in", which happens on the sign-in screen. The
 * nav is not rendered there, so nothing has to decide what to do about it.
 */
export function AdminRoleProvider({
  role,
  grants = null,
  children,
}: {
  role: AdminRole | null;
  grants?: readonly string[] | null;
  children: ReactNode;
}) {
  // Memoised on the contents rather than the array: the layout builds a fresh
  // one on every render, and without this every navigation would hand the nav a
  // new object and rerender all forty rows of it.
  const key = grants === null ? null : grants.join('|');
  const value = useMemo<AdminIdentity>(
    () => ({ role, grants: key === null ? null : key.split('|').filter(Boolean) }),
    [role, key],
  );

  return <IdentityContext.Provider value={value}>{children}</IdentityContext.Provider>;
}

export function useAdminRole(): AdminRole | null {
  return useContext(IdentityContext).role;
}

/** What the signed-in operator may open — `null` when they are not narrowed. */
export function useAdminGrants(): readonly string[] | null {
  return useContext(IdentityContext).grants;
}
