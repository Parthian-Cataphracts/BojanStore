import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { ReportRangePicker } from '@/components/ReportRangePicker';
import { getOrders } from '@/lib/api/orders';
import { getFinancialTotals, getOrderStatusCounts } from '@/lib/api/reports';

export const metadata: Metadata = { title: 'گزارش فروش' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

function rangeToDates(range: string | undefined): { from?: string; to?: string } {
  const to = new Date();
  const from = new Date(to);
  if (range === '7d') from.setDate(from.getDate() - 7);
  else if (range === '90d') from.setDate(from.getDate() - 90);
  else if (range === 'year') { from.setMonth(0); from.setDate(1); }
  else from.setDate(from.getDate() - 30);
  return { from: from.toISOString(), to: to.toISOString() };
}

/** Screen 133 - گزارش فروش. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const { from, to } = rangeToDates(first(params.range));
  // The four figures are aggregates over the whole range. They used to be
  // derived from a page of at most 200 orders, so once a range held more than
  // that, the revenue stopped growing and the cancel rate was measured against
  // a sample rather than the period.
  const [totals, statusCounts, { items: allOrders }] = await Promise.all([
    getFinancialTotals({ from, to }),
    getOrderStatusCounts({ from, to }),
    getOrders({ pageSize: 200, from, to }),
  ]);

  const orders = allOrders.filter((order) => order.status !== 'cancelled');

  const placed = statusCounts.reduce((sum, entry) => sum + entry.count, 0);
  const cancelled = statusCounts.find((entry) => entry.status === 'cancelled')?.count ?? 0;

  // netRevenue already excludes cancelled orders, so it divides by the count
  // that produced it.
  const average = totals.orderCount > 0 ? Math.round(totals.netRevenue / totals.orderCount) : 0;
  const cancelRate = placed > 0 ? Math.round((cancelled / placed) * 1000) / 10 : 0;

  const kpis = [
    { label: 'درآمد بازه', value: formatPrice(totals.netRevenue), icon: 'payments' },
    { label: 'تعداد سفارش', value: toPersianDigits(totals.orderCount), icon: 'shopping_cart' },
    { label: 'میانگین سبد', value: formatPrice(average), icon: 'shopping_basket' },
    { label: 'نرخ لغو', value: `${toPersianDigits(cancelRate)}٪`, icon: 'cancel' },
  ];

  const columns = [
    { key: 'number', header: 'سفارش', cell: (row: (typeof orders)[number]) => row.number },
    { key: 'customer', header: 'مشتری', cell: (row: (typeof orders)[number]) => row.customer },
    { key: 'total', header: 'مبلغ', cell: (row: (typeof orders)[number]) => <span className="tabular">{formatPrice(row.total)}</span> },
  ];

  const rows = orders;

  return (
    <AdminPage
      title="گزارش فروش"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'گزارش‌ها' }, { label: 'فروش' }]}
      actions={
        <Link href="/reports/export" className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}>
          <Icon name="download" size={18} />
          خروجی گرفتن
        </Link>
      }
    >
      <ReportRangePicker />
      <KpiRow items={kpis} />

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="داده‌ای برای این بازه نیست"
        emptyIcon="bar_chart"
      />
    </AdminPage>
  );
}
