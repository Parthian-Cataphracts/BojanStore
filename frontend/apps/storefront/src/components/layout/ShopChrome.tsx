'use client';

import type { ReactNode } from 'react';
import { usePathname } from 'next/navigation';
import { BottomNav } from './BottomNav';
import { isBareRoute, isImmersiveRoute } from '@/lib/chrome';

/**
 * The furniture around a page — everything except the header.
 *
 * A client component reading the path, rather than moving twenty-eight route
 * folders into a `(shop)` group so that four could sit outside it. The rule
 * itself lives in `@/lib/chrome`, which the chat launcher reads too.
 *
 * The footer arrives as a prop rather than being imported here. It reads the
 * shop's settings on the server, and a client component cannot render an async
 * one — passing it in from the layout keeps the path check on the client and
 * the data on the server, with no round trip for either.
 */
export function ShopChrome({ footer }: { footer: ReactNode }) {
  const pathname = usePathname();

  if (isBareRoute(pathname)) return null;

  /*
    The tab bar is `lg:hidden`, and the strip reserved for it collapses to zero
    at the same width — so leaving both off here is a phone-and-tablet change
    and desktop cannot tell the difference. The footer stays: it is content, not
    navigation, and nothing about it is in the way of the buy bar.
  */
  const immersive = isImmersiveRoute(pathname);

  return (
    <>
      {footer}

      {/*
        The bar is `fixed`, so the page has to reserve its height or the last of
        the content sits underneath it. That reservation used to be a literal
        72px, which was never the bar's height: the bar padded itself by
        `env(safe-area-inset-bottom, 20px)` on top of its content, so it stood
        15px taller than this on a phone with no home indicator and 29px taller
        on one with. `--bottom-inset` is the bar's own height, and it collapses
        to zero at `lg` where the bar is hidden — so this no longer needs a
        breakpoint of its own to avoid leaving a dead strip on desktop.
      */}
      {!immersive && (
        <>
          <div aria-hidden className="h-bottom-inset" />
          <BottomNav />
        </>
      )}
    </>
  );
}
