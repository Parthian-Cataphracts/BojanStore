'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn, Icon } from '@bojan/ui';

/**
 * Mobile tab bar, per the phone drawings of screens 92-160.
 *
 * The sidebar is desktop-only; below `md` the design puts four destinations in
 * a fixed bottom bar and everything else behind the drawer in the top bar.
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
      className="glass-nav fixed inset-x-0 bottom-0 z-50 flex h-20 items-center justify-around border-t border-outline-variant/40 px-4 pb-safe md:hidden"
    >
      {tabs.map((tab) => {
        const active = tab.href === '/' ? pathname === '/' : pathname.startsWith(tab.href);

        return (
          <Link
            key={tab.href}
            href={tab.href}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'flex flex-col items-center justify-center rounded-xl transition-all duration-150 active:scale-90',
              active
                ? 'bg-tertiary-fixed/30 px-3 py-1 font-bold text-secondary'
                : 'p-2 text-on-surface-variant hover:bg-surface-container-low',
            )}
          >
            <Icon name={tab.icon} filled={active} />
            <span className="mt-1 text-label-md font-label-md">{tab.label}</span>
          </Link>
        );
      })}
    </nav>
  );
}
