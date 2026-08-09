'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn, Icon } from '@bojan/ui';

/**
 * Mobile tab bar, per the phone drawings of screens 92-160.
 *
 * The sidebar is desktop-only; below `md` the design puts four destinations in
 * a fixed bottom bar and everything else behind the drawer in the top bar.
 *
 * `.bottom-nav` sizes it from `--bottom-inset`, the same variable `AdminShell`
 * pads the content with. The bar used to be `h-20 pb-safe` — 80px of declared
 * height plus a device inset the shell's matching `pb-20` knew nothing about,
 * so the foot of every page sat behind it.
 */
const tabs = [
  { label: 'داشبورد', icon: 'dashboard', href: '/' },
  { label: 'سفارش‌ها', icon: 'shopping_bag', href: '/orders' },
  { label: 'محصولات', icon: 'inventory_2', href: '/products' },
  { label: 'تنظیمات', icon: 'settings', href: '/settings' },
];

export function AdminBottomNav() {
  const pathname = usePathname();

  return (
    <nav
      aria-label="ناوبری پنل مدیریت"
      className="bottom-nav glass-nav z-50 grid grid-cols-4 border-t border-outline-variant/40 px-4 md:hidden"
    >
      {tabs.map((tab) => {
        const active = tab.href === '/' ? pathname === '/' : pathname.startsWith(tab.href);

        return (
          <Link
            key={tab.href}
            href={tab.href}
            aria-current={active ? 'page' : undefined}
            className={cn(
              // One margin for both states rather than a different padding
              // each: the two paddings differed, so an icon stepped up and
              // down by 4px as the operator moved between sections. A grid
              // item stretches to the row on its own, so the margin is all
              // that is needed to inset the active pill.
              'my-2 flex flex-col items-center justify-center gap-1 rounded-xl px-1 transition-colors duration-150 active:scale-90',
              active
                ? 'bg-tertiary-fixed/30 font-bold text-secondary'
                : 'text-on-surface-variant hover:bg-surface-container-low',
            )}
          >
            <Icon name={tab.icon} filled={active} />
            {/* `truncate` needs a width to work against; the grid column is it. */}
            <span className="w-full truncate text-center text-label-md">{tab.label}</span>
          </Link>
        );
      })}
    </nav>
  );
}
