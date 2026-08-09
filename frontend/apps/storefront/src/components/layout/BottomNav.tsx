'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn, Icon } from '@bojan/ui';
import { CartLink } from './CartLink';
import { routes } from '@/lib/routes';

/**
 * The five destinations, in the order the reference shop puts them: the front
 * page, the full catalogue, the catalogue split by department, the basket, and
 * the customer's own corner.
 *
 * Search used to hold the third slot. It moved out because the mobile header
 * already carries a search shortcut of its own (`SiteHeader`, hidden from `lg`
 * up), so the tab bar was spending one of five slots on a destination that was
 * one tap away regardless — while the product listing, the page most of the
 * shop links into, had no slot at all.
 */
const tabs = [
  { label: 'خانه', icon: 'home', href: routes.home },
  { label: 'محصولات', icon: 'inventory_2', href: routes.products },
  { label: 'دسته‌بندی‌ها', icon: 'grid_view', href: routes.categories },
  { label: 'سبد خرید', icon: 'shopping_cart', href: routes.cart },
  // The account tab carries the shop's name, not the word "account" — the
  // reference does the same, and it reads as somewhere that belongs to you
  // rather than a settings screen.
  { label: 'بوژان من', icon: 'person', href: routes.account },
];

/**
 * Mobile tab bar. Hidden from `lg` up, where the header nav takes over.
 *
 * `.bottom-nav` fixes it to the bottom edge and sizes it from
 * `--bottom-inset` — the same variable `ShopChrome` reserves space with and
 * that sticky bars and the chat launcher sit on top of. Nothing here repeats
 * the height as a literal, which is what let the bar and the space kept for it
 * disagree before.
 *
 * Five equal columns rather than `justify-around`: the labels are different
 * lengths — «بوژان من» is nearly three times «خانه» — and space-around
 * distributes the slack between them, so the icons sat at five uneven
 * positions that shifted again in the other orientation.
 */
export function BottomNav() {
  const pathname = usePathname();

  return (
    <nav
      aria-label="ناوبری اصلی"
      className="bottom-nav glass-nav z-50 grid grid-cols-5 border-t border-outline-variant/40 lg:hidden"
    >
      {tabs.map((tab) => {
        const active = tab.href === routes.home ? pathname === '/' : pathname.startsWith(tab.href);
        const className = cn(
          'flex h-full flex-col items-center justify-center gap-1 px-1 transition-colors active:scale-90',
          active
            ? 'font-bold text-primary'
            : 'font-medium text-on-surface-variant hover:text-primary',
        );
        // `truncate` needs a width to work against, and a grid column gives it
        // one — without it the longest label widened its own column and pushed
        // the other four out of alignment.
        const label = <span className="w-full truncate text-center text-[11px] leading-tight">{tab.label}</span>;

        // The cart tab carries the count, so it comes from the shared link.
        if (tab.href === routes.cart) {
          return (
            <CartLink key={tab.href} iconName={tab.icon} className={className}>
              {label}
            </CartLink>
          );
        }

        return (
          <Link
            key={tab.href}
            href={tab.href}
            aria-current={active ? 'page' : undefined}
            className={className}
          >
            <Icon name={tab.icon} filled={active} />
            {label}
          </Link>
        );
      })}
    </nav>
  );
}
