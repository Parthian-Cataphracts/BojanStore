import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { ReportRangePicker } from '@/components/ReportRangePicker';
import { getOrders } from '@/lib/api/orders';
import { resolveRange } from '@/lib/report-range';

export const metadata: Metadata = { title: 'گزارش فروش' };

type SearchParams = Record<string, string | string[] | undefined>;

/** Screen 133 - گزارش فروش. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const { from, to } = resolveRange(params.range);
  const { items: allOrders } = await getOrders({ pageSize: 200, from, to });
  const orders = allOrders.filter((order) => order.status !== 'cancelled');
  const revenue = orders.reduce((sum, order) => sum + order.total, 0);
  const average = orders.length > 0 ? Math.round(revenue / orders.length) : 0;
  const cancelRate = allOrders.length > 0
    ? Math.round(((allOrders.length - orders.length) / allOrders.length) * 1000) / 10
    : 0;

  const kpis = [
    { label: 'درآمد بازه', value: formatPrice(revenue), icon: 'payments' },
    { label: 'تعداد سفارش', value: toPersianDigits(orders.length), icon: 'shopping_cart' },
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
        emptyIcon="analytics"
      />
    </AdminPage>
  );
}
