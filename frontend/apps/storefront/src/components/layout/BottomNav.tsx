'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn, Icon } from '@bojan/ui';
import { CartLink } from './CartLink';
import { routes } from '@/lib/routes';

const tabs = [
  { label: 'خانه', icon: 'home', href: routes.home },
  { label: 'دسته‌بندی‌ها', icon: 'category', href: routes.categories },
  { label: 'جستجو', icon: 'search', href: routes.search },
  { label: 'سبد خرید', icon: 'shopping_cart', href: routes.cart },
  { label: 'حساب کاربری', icon: 'person', href: routes.account },
];

/** Mobile-only tab bar. Hidden from `md` up, where the header nav takes over. */
export function BottomNav() {
  const pathname = usePathname();

  return (
    <nav
      aria-label="ناوبری اصلی"
      className="glass-nav fixed bottom-0 z-50 flex w-full items-center justify-around border-t border-outline-variant/40 px-2 py-2 pb-safe lg:hidden"
    >
      {tabs.map((tab) => {
        const active = tab.href === routes.home ? pathname === '/' : pathname.startsWith(tab.href);
        const className = cn(
          'flex flex-col items-center justify-center rounded-lg p-2 transition-colors active:scale-90',
          active ? 'font-bold text-primary' : 'text-on-surface-variant hover:text-primary',
        );
        const label = <span className="mt-1 text-[11px] font-medium leading-tight">{tab.label}</span>;

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
            <Icon name={tab.icon} filled={active} className="mb-1" />
            <span className="text-[11px] font-medium leading-tight">{tab.label}</span>
          </Link>
        );
      })}
    </nav>
  );
}
