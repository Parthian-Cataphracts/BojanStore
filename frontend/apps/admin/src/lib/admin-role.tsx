'use client';

import { createContext, useContext, type ReactNode } from 'react';
import type { AdminRole } from '@/lib/auth/session';

const RoleContext = createContext<AdminRole | null>(null);

/**
 * The signed-in operator's role, for the parts of the panel that have to draw
 * differently depending on it.
 *
 * A context rather than a prop because the one consumer, `AdminNavList`, is
 * rendered twice from two different places — the desktop rail and the mobile
 * drawer — and the drawer hangs off `AdminTopBar`, which every page renders for
 * itself. Threading a role down that path would have meant a new prop on four
 * components that have no use for it, which is the same reason
 * `SidebarStateProvider` exists.
 *
 * `null` means "not signed in", which happens on the sign-in screen. The nav is
 * not rendered there, so nothing has to decide what to do about it.
 */
export function AdminRoleProvider({
  role,
  children,
}: {
  role: AdminRole | null;
  children: ReactNode;
}) {
  return <RoleContext.Provider value={role}>{children}</RoleContext.Provider>;
}

export function useAdminRole(): AdminRole | null {
  return useContext(RoleContext);
}
