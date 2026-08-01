import Link from 'next/link';
import { Icon } from '@bojan/ui';
import { CartLink } from './CartLink';
import { routes } from '@/lib/routes';

const primaryNav = [
  { label: 'خانه', href: routes.home },
  { label: 'دسته‌بندی‌ها', href: routes.categories },
  { label: 'جدیدترین‌ها', href: `${routes.products}?sort=newest` },
  { label: 'پرطرفدارها', href: `${routes.products}?sort=bestselling` },
  { label: 'هدیه و سبک زندگی', href: routes.category('gift-lifestyle') },
  { label: 'مجله', href: routes.magazine },
  { label: 'سازمانی', href: routes.business },
];

/**
 * Sticky translucent app bar.
 *
 * The design draws two variants: a 56px mobile bar (cart · wordmark · search)
 * and an 80px desktop bar with the full nav. Both live here, switched at `md`.
 */
export function SiteHeader() {
  return (
    <header className="glass-nav sticky top-0 z-50 w-full border-b border-outline-variant/30">
      <div className="mx-auto flex h-14 max-w-shell items-center justify-between px-margin-mobile md:h-20 md:px-margin-desktop">
        {/* Mobile: cart shortcut. Desktop: hidden in favour of the actions cluster. */}
        <CartLink
          iconName="shopping_bag"
          className="text-primary transition-opacity hover:opacity-80 active:scale-95 md:hidden"
        />

        <Link
          href={routes.home}
          className="font-headline text-headline-lg-mobile font-bold tracking-tight text-primary-container md:text-headline-lg"
        >
          بوژان
        </Link>

        <nav className="hidden items-center gap-lg md:flex">
          {primaryNav.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className="text-label-md font-medium text-on-surface-variant transition-colors duration-300 hover:text-secondary-container"
            >
              {item.label}
            </Link>
          ))}
        </nav>

        {/* Mobile: search shortcut only — the rest lives in the bottom nav. */}
        <Link
          href={routes.search}
          aria-label="جستجو"
          className="text-primary transition-opacity hover:opacity-80 active:scale-95 md:hidden"
        >
          <Icon name="search" />
        </Link>

        <div className="hidden items-center gap-gutter md:flex">
          <Link
            href={routes.search}
            aria-label="جستجو"
            className="p-sm text-primary-container transition-colors hover:text-secondary-container"
          >
            <Icon name="search" />
          </Link>
          <Link
            href={routes.account}
            aria-label="حساب کاربری"
            className="p-sm text-primary-container transition-colors hover:text-secondary-container"
          >
            <Icon name="person" />
          </Link>
          <CartLink className="p-sm text-primary-container transition-colors hover:text-secondary-container" />
        </div>
      </div>
    </header>
  );
}
