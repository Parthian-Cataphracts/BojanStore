import Link from 'next/link';
import { Badge, Card, Icon, formatNumber, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminTopBar } from '@/components/AdminTopBar';
import { getDashboardKpis } from '@/lib/api/dashboard';
import { getOrders } from '@/lib/api/orders';
import { orderStatusMeta } from '@/lib/status';
import type { AdminOrderStatus } from '@/lib/types';

// No searchParams to force dynamic rendering, and the data is live — do not
// let `next build` try to prerender this against a backend that may not be up.
export const dynamic = 'force-dynamic';

/**
 * Screen 92 — Admin dashboard.
 *
 * The remaining 69 admin screens (93–160) follow this shell: `AdminTopBar` for
 * the title row, cards on `bg-background`, tables inside `Card surface="plain"`.
 */
export default async function AdminDashboardPage() {
  const [dashboard, { items: recentOrders }] = await Promise.all([
    getDashboardKpis(),
    getOrders({ pageSize: 4 }),
  ]);

  const kpis = [
    { label: 'فروش امروز', value: formatPrice(dashboard.revenueToday), delta: '', icon: 'payments', up: true },
    { label: 'سفارش‌های جدید', value: toPersianDigits(dashboard.ordersToday), delta: '', icon: 'shopping_cart', up: true },
    { label: 'مشتریان جدید', value: toPersianDigits(dashboard.newCustomersThisMonth), delta: '', icon: 'group', up: true },
    {
      label: 'کالاهای رو به اتمام',
      value: toPersianDigits(dashboard.lowStockProducts),
      delta: 'نیاز به بررسی',
      icon: 'warehouse',
      up: false,
    },
  ];

  return (
    <>
      <AdminTopBar title="داشبورد ادمین" />

      <main className="flex flex-col gap-lg p-lg pt-[88px]">
        <section className="grid grid-cols-1 gap-md sm:grid-cols-2 xl:grid-cols-4">
          {kpis.map((kpi) => (
            <Card key={kpi.label} className="flex flex-col gap-sm p-lg">
              <div className="flex items-center justify-between">
                <span className="flex h-10 w-10 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
                  <Icon name={kpi.icon} size={22} />
                </span>
                <Badge tone={kpi.up ? 'mint' : 'warning'}>{kpi.delta}</Badge>
              </div>
              <span className="text-caption text-on-surface-variant">{kpi.label}</span>
              <span className="tabular text-kpi-mobile text-primary md:text-kpi">{kpi.value}</span>
            </Card>
          ))}
        </section>

        <section className="grid gap-lg xl:grid-cols-[2fr_1fr]">
          <Card surface="plain" className="overflow-hidden">
            <header className="flex items-center justify-between border-b border-outline-variant/40 px-lg py-md">
              <h2 className="font-headline text-card-title text-primary md:text-section-title">آخرین سفارش‌ها</h2>
              <Link
                href="/orders"
                className="text-label-md font-semibold text-secondary hover:text-primary"
              >
                مشاهده همه
              </Link>
            </header>

            <div className="overflow-x-auto">
              <table className="w-full text-start">
                <thead className="bg-surface-container-low text-on-surface-variant">
                  <tr>
                    <th scope="col" className="px-lg py-sm text-start">شماره سفارش</th>
                    <th scope="col" className="px-lg py-sm text-start">مشتری</th>
                    <th scope="col" className="px-lg py-sm text-start">مبلغ</th>
                    <th scope="col" className="px-lg py-sm text-start">وضعیت</th>
                  </tr>
                </thead>
                <tbody>
                  {recentOrders.map((order) => {
                    const meta = orderStatusMeta[order.status as AdminOrderStatus];
                    return (
                      <tr
                        key={order.number}
                        className="border-t border-outline-variant/30 transition-colors hover:bg-surface-container-low"
                      >
                        <td className="tabular px-lg py-md text-primary">{order.number}</td>
                        <td className="px-lg py-md text-on-surface">{order.customer}</td>
                        <td className="tabular px-lg py-md text-on-surface">{formatPrice(order.total)}</td>
                        <td className="px-lg py-md">
                          <Badge tone={meta.tone}>{meta.label}</Badge>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </Card>

          <Card className="flex flex-col gap-md p-lg">
            <h2 className="font-headline text-card-title text-primary md:text-section-title">خلاصه ماه</h2>

            <dl className="flex flex-col gap-md">
              {[
                { label: 'درآمد ماه جاری', value: formatPrice(dashboard.revenueThisMonth) },
                { label: 'تعداد سفارش', value: formatNumber(dashboard.ordersThisMonth) },
                {
                  label: 'میانگین سبد خرید',
                  value: formatPrice(
                    dashboard.ordersThisMonth > 0
                      ? Math.round(dashboard.revenueThisMonth / dashboard.ordersThisMonth)
                      : 0,
                  ),
                },
                { label: 'در انتظار پردازش', value: formatNumber(dashboard.pendingOrders) },
              ].map((row) => (
                <div
                  key={row.label}
                  className="flex items-center justify-between border-b border-outline-variant/30 pb-sm last:border-0 last:pb-0"
                >
                  <dt className="text-caption text-on-surface-variant">{row.label}</dt>
                  <dd className="tabular text-body-md font-label-md text-primary">{row.value}</dd>
                </div>
              ))}
            </dl>
          </Card>
        </section>
      </main>
    </>
  );
}
