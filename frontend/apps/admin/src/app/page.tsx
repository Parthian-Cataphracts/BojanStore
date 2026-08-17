import Link from 'next/link';
import { redirect } from 'next/navigation';
import { Badge, Card, Icon, formatNumber, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminTopBar } from '@/components/AdminTopBar';
import { requireAdminSession } from '@/lib/auth/server';
import { firstOpenPath, reachesSection } from '@/lib/permissions';
import { ServerStatusCard } from '@/components/ServerStatusCard';
import { getDashboardKpis } from '@/lib/api/dashboard';
import { getOrders } from '@/lib/api/orders';
import { getServerStatus } from '@/lib/api/settings';
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
  /*
    Somewhere else, for an operator this screen has nothing to show.

    The dashboard is in everybody's menu because it is where the panel opens,
    but every figure on it comes from the reports endpoints — so an operator
    narrowed to, say, the returns queue landed here on four failed panels and a
    connection error, which reads as a broken panel rather than as a permission
    they were not given. They go to the first screen that is theirs instead.
  */
  const session = await requireAdminSession();
  const grants = session.sections?.length ? session.sections : null;
  if (!reachesSection(grants, 'reports')) redirect(firstOpenPath(grants));

  const [dashboard, { items: recentOrders }, serverStatus] = await Promise.all([
    getDashboardKpis(),
    getOrders({ pageSize: 4 }),
    getServerStatus(),
  ]);

  const kpis = [
    {
      label: 'فروش امروز',
      value: formatPrice(dashboard.revenueToday),
      delta: '',
      icon: 'payments',
      up: true,
    },
    {
      label: 'سفارش‌های جدید',
      value: toPersianDigits(dashboard.ordersToday),
      delta: '',
      icon: 'shopping_cart',
      up: true,
    },
    {
      label: 'مشتریان جدید',
      value: toPersianDigits(dashboard.newCustomersThisMonth),
      delta: '',
      icon: 'group',
      up: true,
    },
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

      <main className="gap-lg p-lg flex flex-col pt-[88px]">
        {/*
          First, not last. This is the one card on the dashboard that answers
          «is anything wrong right now» — database reachable, disk, memory,
          uptime — and it sat under the fold beneath the KPIs and two tables,
          which is the one place a health panel is no use: it is read when
          something is suspected, and by then nobody scrolls for it.
        */}
        {serverStatus && <ServerStatusCard status={serverStatus} />}

        <section className="gap-md grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4">
          {kpis.map((kpi) => (
            <Card key={kpi.label} className="gap-sm p-lg flex flex-col">
              <div className="flex items-center justify-between">
                <span className="bg-primary-fixed-dim/20 text-primary-container flex h-10 w-10 items-center justify-center rounded-full">
                  <Icon name={kpi.icon} size={22} />
                </span>
                <Badge tone={kpi.up ? 'mint' : 'warning'}>{kpi.delta}</Badge>
              </div>
              <span className="text-caption text-on-surface-variant">{kpi.label}</span>
              <span className="tabular text-kpi text-primary">{kpi.value}</span>
            </Card>
          ))}
        </section>

        <section className="gap-lg grid xl:grid-cols-[2fr_1fr]">
          <Card surface="plain" className="overflow-hidden">
            <header className="border-outline-variant/40 px-lg py-md flex items-center justify-between border-b">
              <h2 className="font-headline text-section-title text-primary">آخرین سفارش‌ها</h2>
              <Link
                href="/orders"
                className="text-label-md text-secondary hover:text-primary font-semibold"
              >
                مشاهده همه
              </Link>
            </header>

            <div className="overflow-x-auto">
              <table className="w-full text-start">
                <thead className="bg-surface-container-low text-on-surface-variant">
                  <tr>
                    <th scope="col" className="px-lg py-sm text-start">
                      شماره سفارش
                    </th>
                    <th scope="col" className="px-lg py-sm text-start">
                      مشتری
                    </th>
                    <th scope="col" className="px-lg py-sm text-start">
                      مبلغ
                    </th>
                    <th scope="col" className="px-lg py-sm text-start">
                      وضعیت
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {recentOrders.map((order) => {
                    const meta = orderStatusMeta[order.status as AdminOrderStatus];
                    return (
                      <tr
                        key={order.number}
                        className="border-outline-variant/30 hover:bg-surface-container-low border-t transition-colors"
                      >
                        <td className="tabular px-lg py-md text-primary">{order.number}</td>
                        <td className="px-lg py-md text-on-surface">{order.customer}</td>
                        <td className="tabular px-lg py-md text-on-surface">
                          {formatPrice(order.total)}
                        </td>
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

          <Card className="gap-md p-lg flex flex-col">
            <h2 className="font-headline text-section-title text-primary">خلاصه ماه</h2>

            <dl className="gap-md flex flex-col">
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
                  className="border-outline-variant/30 pb-sm flex items-center justify-between border-b last:border-0 last:pb-0"
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
