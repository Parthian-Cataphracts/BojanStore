'use client';

import { usePathname } from 'next/navigation';
import type { ReactNode } from 'react';
import { cn } from '@bojan/ui';
import { AdminBottomNav } from './AdminBottomNav';
import { AdminSidebar } from './AdminSidebar';
import { SidebarStateProvider, useSidebarState } from '@/lib/sidebar-state';
import { AdminRoleProvider } from '@/lib/admin-role';
import type { AdminRole } from '@/lib/auth/session';

/** Routes rendered without the panel chrome — sign-in and its sub-flows. */
const BARE_ROUTES = ['/login'];

/**
 * Chooses between the full panel (sidebar + content rail) and a bare canvas.
 * The sign-in screen must not show navigation to a signed-out visitor.
 */
export function AdminShell({
  children,
  role,
  grants = null,
}: {
  children: ReactNode;
  role: AdminRole | null;
  /** What this operator may open, or `null` when nobody has narrowed them. */
  grants?: readonly string[] | null;
}) {
  const pathname = usePathname();
  const bare = BARE_ROUTES.some((route) => pathname === route || pathname.startsWith(`${route}/`));

  if (bare) return <>{children}</>;

  return (
    <AdminRoleProvider role={role} grants={grants}>
      <SidebarStateProvider>
        <AdminShellChrome>{children}</AdminShellChrome>
      </SidebarStateProvider>
    </AdminRoleProvider>
  );
}

function AdminShellChrome({ children }: { children: ReactNode }) {
  const { collapsed, toggle } = useSidebarState();

  return (
    <>
      <AdminSidebar collapsed={collapsed} onToggle={toggle} />
      {/*
        Content sits inside the sidebar rail on desktop and clears the mobile
        tab bar below it. `ms-*` is the logical form of the physical margin the
        RTL rail needs — it renders identically and survives a direction change.

        The clearance was `pb-20`, matching the bar's `h-20` but not the
        `pb-safe` the bar adds underneath it, so the last 20px of every page —
        34px on a notched phone — sat behind the tab bar. `--bottom-inset` is
        the bar's real height and is already zero at `md`, which is where the
        separate `md:pb-0` went.
      */}
      <div
        className={cn(
          'pb-bottom-inset transition-[margin] duration-200',
          collapsed ? 'md:ms-20' : 'md:ms-64',
        )}
      >
        {children}
      </div>
      <AdminBottomNav />
    </>
  );
}
