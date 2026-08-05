'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useEffect, useState } from 'react';
import { Icon, cn, toPersianDigits } from '@bojan/ui';
import { routes } from '@/lib/routes';

/**
 * Notification bell with an unread badge.
 *
 * Fetched on the client, not rendered from the session on the server. The
 * header sits in the root layout, so reading the session cookie to draw this
 * would opt *every* page into dynamic rendering — including the product and
 * category pages that are statically generated. A badge is not worth that, and
 * a count that appears a moment after paint is not worth noticing.
 *
 * The count is re-read on navigation rather than polled on a timer: a shopper
 * moving around the site refreshes it as they go, and one sitting still on a
 * page is not somewhere a background request every few seconds earns its keep.
 * Opening the notifications screen marks things read, so returning from it
 * clears the badge on the next navigation.
 */
export function NotificationBell({ className }: { className?: string }) {
  const [count, setCount] = useState(0);
  const pathname = usePathname();

  useEffect(() => {
    // Guards against a slow response landing after the shopper has navigated
    // again and set a newer count.
    let current = true;

    fetch('/api/account/unread-count', { cache: 'no-store' })
      .then((response) => (response.ok ? response.json() : { count: 0 }))
      .then((result: { count?: number }) => {
        if (current) setCount(Number(result.count) || 0);
      })
      .catch(() => {
        // No badge is the same thing the header showed before this resolved.
      });

    return () => {
      current = false;
    };
  }, [pathname]);

  return (
    <Link
      href={routes.notifications}
      aria-label={count > 0 ? `اعلان‌ها، ${toPersianDigits(count)} خوانده‌نشده` : 'اعلان‌ها'}
      className={cn('relative', className)}
    >
      <span className="relative inline-flex">
        <Icon name="notifications" />

        {count > 0 && (
          <span
            aria-hidden="true"
            className={cn(
              'absolute -top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-secondary px-1 text-[10px] font-bold leading-none text-on-secondary',
              // Off the icon's end corner, so it mirrors with the direction —
              // the same placement the cart badge uses.
              '-end-1.5',
            )}
          >
            {/* Past two digits the badge grows wider than the icon it sits on. */}
            {count > 99 ? '۹۹+' : toPersianDigits(count)}
          </span>
        )}
      </span>
    </Link>
  );
}
