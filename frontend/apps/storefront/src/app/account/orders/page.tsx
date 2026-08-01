import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import {
  Badge,
  Card,
  EmptyState,
  Icon,
  buttonClasses,
  Code,
  formatDate,
  formatPrice,
  toPersianDigits,
} from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getOrders } from '@/lib/api/account';
import { orderStatusMeta } from '@/lib/mock/orders';
import type { OrderStatus } from '@/lib/api/types';
import { first, type SearchParams } from '@/lib/search-params';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'سفارش‌های من',
  robots: { index: false },
};

const filters: { value: OrderStatus | 'all'; label: string }[] = [
  { value: 'all', label: 'همه' },
  { value: 'processing', label: 'در حال پردازش' },
  { value: 'shipped', label: 'ارسال شده' },
  { value: 'delivered', label: 'تحویل شده' },
  { value: 'cancelled', label: 'لغو شده' },
];

/** Screen 12 — My orders. */
export default async function OrdersPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const active = (first(params.status) as OrderStatus | undefined) ?? 'all';
  const orders = await getOrders(active === 'all' ? undefined : active);

  return (
    <Container className="py-lg md:py-xl">
      <PageHeader title="سفارش‌های من" backHref={routes.account} />

      {/* Status filter chips */}
      <nav
        aria-label="فیلتر وضعیت"
        className="hide-scrollbar -mx-margin-mobile mb-lg flex gap-sm overflow-x-auto px-margin-mobile pb-sm md:mx-0 md:px-0"
      >
        {filters.map((filter) => (
          <Link
            key={filter.value}
            href={filter.value === 'all' ? routes.orders : `${routes.orders}?status=${filter.value}`}
            className={`shrink-0 rounded-full px-4 py-2 text-label-md font-label-md transition-colors ${
              active === filter.value
                ? 'bg-tertiary-fixed text-on-tertiary-fixed'
                : 'bg-surface-container-high text-on-surface-variant hover:bg-surface-container-highest'
            }`}
          >
            {filter.label}
          </Link>
        ))}
      </nav>

      {orders.length > 0 ? (
        <ul className="flex flex-col gap-md">
          {orders.map((order) => {
            const status = orderStatusMeta[order.status];
            const thumbs = order.thumbnails ?? [];

            return (
              <li key={order.id}>
                <Card className="flex flex-col gap-sm p-md">
                  <div className="flex items-center justify-between border-b border-surface-variant pb-2">
                    <span className="tabular text-label-md font-label-md text-primary">
                      سفارش <Code>#{order.number}</Code>
                    </span>
                    <Badge tone={status.tone}>{status.label}</Badge>
                  </div>

                  <div className="flex items-center justify-between py-2 text-body-md text-on-surface-variant">
                    <span className="flex items-center gap-xs">
                      <Icon name="calendar_today" size={18} />
                      <span className="tabular">{formatDate(order.placedAt, 'long')}</span>
                    </span>
                    <span className="tabular text-label-md font-label-md text-on-background">
                      {formatPrice(order.total)}
                    </span>
                  </div>

                  {thumbs.length > 0 && (
                    <div className="hide-scrollbar flex gap-2 overflow-x-auto py-2">
                      {thumbs.slice(0, 3).map((thumb, index) => (
                        <div
                          key={`${order.id}-${index}`}
                          className="relative h-16 w-16 shrink-0 overflow-hidden rounded border border-outline-variant"
                        >
                          <Image src={thumb} alt="" fill sizes="64px" className="object-cover" />
                        </div>
                      ))}
                      {thumbs.length > 3 && (
                        <span className="tabular flex h-16 w-16 shrink-0 items-center justify-center rounded border border-outline-variant bg-surface-container text-caption text-on-surface-variant">
                          +{toPersianDigits(thumbs.length - 3)} کالا
                        </span>
                      )}
                    </div>
                  )}

                  <Link
                    href={`${routes.orders}/${order.id}`}
                    className={buttonClasses({
                      variant: 'outline',
                      fullWidth: true,
                      className: 'mt-2 gap-2',
                    })}
                  >
                    مشاهده جزئیات
                    <Icon name="visibility" size={18} />
                  </Link>
                </Card>
              </li>
            );
          })}
        </ul>
      ) : (
        <EmptyState
          icon="receipt_long"
          title="سفارشی با این وضعیت ندارید"
          description="وقتی سفارشی ثبت کنید، اینجا می‌توانید وضعیتش را دنبال کنید."
          action={
            <Link href={routes.products} className={buttonClasses()}>
              شروع خرید
            </Link>
          }
        />
      )}
    </Container>
  );
}
