'use client';

import { usePathname } from 'next/navigation';
import { SiteFooter } from './SiteFooter';
import { BottomNav } from './BottomNav';

/**
 * The furniture around a page — everything except the header.
 *
 * Signing in, registering and recovering a password are single-task screens.
 * A footer full of shipping terms and a tab bar offering four other places to
 * go are exit ramps from the one thing the customer came to do, and on a phone
 * they take a third of the screen the form needs. So these routes get neither.
 *
 * A client component reading the path, rather than moving twenty-eight route
 * folders into a `(shop)` group so that four could sit outside it. The list
 * below is the whole rule and it is greppable; a route group would spread the
 * same decision across the filesystem.
 */

/** Prefixes with no shop furniture. Matched on segment boundaries, not substrings. */
const BARE_ROUTES = ['/login', '/register', '/forgot-password', '/reset-password'];

export function ShopChrome() {
  const pathname = usePathname();

  const bare = BARE_ROUTES.some(
    (route) => pathname === route || pathname.startsWith(`${route}/`),
  );

  if (bare) return null;

  return (
    <>
      <SiteFooter />

      {/*
        The bar is `fixed`, so it needs the page to reserve its height or the
        last of the content sits underneath it. That reservation used to live
        on <body>, which meant a dead 72px strip on every screen that hides the
        bar — including these. Kept with the bar it belongs to instead.
      */}
      <div aria-hidden className="h-[72px] lg:hidden" />
      <BottomNav />
    </>
  );
}
