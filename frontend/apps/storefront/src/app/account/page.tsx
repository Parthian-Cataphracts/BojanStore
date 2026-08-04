import type { Metadata } from 'next';
import Link from 'next/link';
import {
  Badge,
  Card,
  Code,
  Icon,
  formatDate,
  formatNumber,
  formatPrice,
  toPersianDigits,
} from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { SignOutButton } from '@/components/account/SignOutButton';
import { getCurrentUser, getOrders } from '@/lib/api/account';
import { orderStatusMeta } from '@/lib/mock/orders';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'حساب کاربری من',
  robots: { index: false },
};

const menu = [
  { label: 'سفارش‌های من', icon: 'receipt_long', href: routes.orders },
  { label: 'علاقه‌مندی‌ها', icon: 'favorite', href: routes.wishlist },
  { label: 'آدرس‌های من', icon: 'place', href: routes.addresses },
  { label: 'مرجوعی‌های من', icon: 'assignment_return', href: routes.myReturns },
  { label: 'اطلاعات شخصی', icon: 'person', href: routes.profile },
  { label: 'کیف پول و اعتبار', icon: 'account_balance_wallet', href: routes.wallet },
  { label: 'کدهای تخفیف', icon: 'sell', href: routes.coupons },
  { label: 'نظرات من', icon: 'rate_review', href: routes.reviews },
  { label: 'اعلان‌های من', icon: 'notifications', href: routes.notifications },
  { label: 'پیام‌های پشتیبانی', icon: 'support_agent', href: routes.support },
  { label: 'دیده‌شده اخیر', icon: 'history', href: routes.recentlyViewed },
  { label: 'مقایسه محصولات', icon: 'compare_arrows', href: routes.compare },
  { label: 'پیگیری سفارش', icon: 'travel_explore', href: routes.track },
];

/** Screen 10 — My account. */
export default async function AccountPage() {
  const [user, allOrders] = await Promise.all([getCurrentUser(), getOrders()]);
  const orders = allOrders.slice(0, 3);

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      {/* Profile header */}
      <Card className="flex flex-wrap items-center gap-md p-lg">
        <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-soft-mint font-headline text-display-md text-primary">
          {user.firstName.charAt(0)}
        </span>

        <div className="flex min-w-0 flex-1 flex-col gap-xs">
          <h1 className="font-headline text-display-md text-primary">
            {user.firstName} {user.lastName}
          </h1>
          <span className="tabular text-caption text-on-surface-variant">
            {toPersianDigits(user.phone)}
          </span>
        </div>

        <Link
          href={routes.profile}
          className="inline-flex shrink-0 items-center gap-xs text-label-md font-label-md text-secondary transition-colors hover:text-primary"
        >
          ویرایش
          <Icon name="edit" size={18} />
        </Link>
      </Card>

      {/* Wallet + loyalty */}
      <div className="grid grid-cols-2 gap-md">
        <Card className="flex flex-col gap-xs p-lg">
          <span className="flex items-center gap-xs text-caption text-on-surface-variant">
            <Icon name="account_balance_wallet" size={18} />
            موجودی کیف پول
          </span>
          <span className="tabular text-body-lg font-label-md text-primary">
            {formatPrice(user.walletBalance)}
          </span>
        </Card>

        <Card className="flex flex-col gap-xs p-lg">
          <span className="flex items-center gap-xs text-caption text-on-surface-variant">
            <Icon name="workspace_premium" size={18} />
            امتیاز باشگاه مشتریان
          </span>
          <span className="tabular text-body-lg font-label-md text-primary">
            {formatNumber(user.loyaltyPoints)}
          </span>
        </Card>
      </div>

      {/* Recent orders */}
      <section className="flex flex-col gap-md">
        <div className="flex items-baseline justify-between gap-md">
          <h2 className="font-headline text-display-md text-primary">سفارش‌های اخیر</h2>
          <Link
            href={routes.orders}
            className="inline-flex items-center gap-xs text-label-md font-label-md text-secondary transition-colors hover:text-primary"
          >
            مشاهده همه
            <Icon name="chevron_left" size={18} />
          </Link>
        </div>

        <ul className="flex flex-col gap-md">
          {orders.map((order) => {
            const status = orderStatusMeta[order.status];
            return (
              <li key={order.id}>
                <Link href={`${routes.orders}/${order.id}`} className="block">
                  <Card className="flex flex-wrap items-center gap-md p-lg transition-shadow hover:shadow-soft">
                    <div className="flex min-w-0 flex-1 flex-col gap-xs">
                      <span className="tabular text-label-md font-label-md text-primary">
                        سفارش <Code>{order.number}</Code>
                      </span>
                      <span className="tabular text-caption text-on-surface-variant">
                        {formatDate(order.placedAt, 'long')} · {toPersianDigits(order.itemCount)} کالا
                      </span>
                    </div>

                    <Badge tone={status.tone}>{status.label}</Badge>

                    <span className="tabular shrink-0 text-body-md font-label-md text-primary">
                      {formatPrice(order.total)}
                    </span>
                  </Card>
                </Link>
              </li>
            );
          })}
        </ul>
      </section>

      {/* Account menu */}
      <nav className="grid grid-cols-2 gap-md md:grid-cols-4">
        {menu.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className="paper-card flex flex-col items-center gap-sm rounded-lg p-lg text-center transition-shadow hover:shadow-soft"
          >
            <span className="flex h-12 w-12 items-center justify-center rounded-full bg-primary-fixed-dim/20">
              <Icon name={item.icon} size={24} className="text-primary-container" />
            </span>
            <span className="text-label-md font-label-md text-primary-container">{item.label}</span>
          </Link>
        ))}
      </nav>

      <SignOutButton />
    </Container>
  );
}
