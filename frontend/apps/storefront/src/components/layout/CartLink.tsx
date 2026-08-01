'use client';

import Link from 'next/link';
import { Icon, cn, toPersianDigits } from '@bojan/ui';
import { useCart } from '@/lib/cart/store';
import { routes } from '@/lib/routes';

/**
 * Cart shortcut with a count.
 *
 * The design draws the icon bare, from a time when adding to the cart did
 * nothing. Now that it does, a basket with items in it and no indication
 * anywhere leaves the shopper guessing whether the click registered — the
 * count is the acknowledgement the interface otherwise never gives.
 *
 * It renders nothing until the cart has hydrated: the count lives in the
 * browser, so drawing it during the server pass would make the first client
 * render disagree with the HTML.
 */
export function CartLink({
  className,
  iconName = 'shopping_cart',
  children,
}: {
  className?: string;
  iconName?: string;
  /** Label for the tab bar; the header shows the icon alone. */
  children?: React.ReactNode;
}) {
  const { count, hydrated } = useCart();
  const showCount = hydrated && count > 0;

  return (
    <Link
      href={routes.cart}
      aria-label={showCount ? `سبد خرید، ${toPersianDigits(count)} کالا` : 'سبد خرید'}
      className={cn('relative', className)}
    >
      <span className="relative inline-flex">
        <Icon name={iconName} />

        {showCount && (
          <span
            aria-hidden="true"
            className={cn(
              'absolute -top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-secondary px-1 text-[10px] font-bold leading-none text-on-secondary',
              // Sits off the icon's end corner, which mirrors with direction.
              '-end-1.5',
            )}
          >
            {/* Past two digits the badge would grow wider than the icon. */}
            {count > 99 ? '۹۹+' : toPersianDigits(count)}
          </span>
        )}
      </span>

      {children}
    </Link>
  );
}
