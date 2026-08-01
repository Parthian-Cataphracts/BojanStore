'use client';

import { usePathname } from 'next/navigation';
import type { ReactNode } from 'react';
import { AdminBottomNav } from './AdminBottomNav';
import { AdminSidebar } from './AdminSidebar';

/** Routes rendered without the panel chrome — sign-in and its sub-flows. */
const BARE_ROUTES = ['/login'];

/**
 * Chooses between the full panel (sidebar + content rail) and a bare canvas.
 * The sign-in screen must not show navigation to a signed-out visitor.
 */
export function AdminShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const bare = BARE_ROUTES.some((route) => pathname === route || pathname.startsWith(`${route}/`));

  if (bare) return <>{children}</>;

  return (
    <>
      <AdminSidebar />
      {/*
        Content sits inside the sidebar rail on desktop and clears the mobile
        tab bar below it. `ms-64` is the logical form of the physical margin the
        RTL rail needs — it renders identically and survives a direction change.
      */}
      <div className="pb-20 md:ms-64 md:pb-0">{children}</div>
      <AdminBottomNav />
    </>
  );
}
