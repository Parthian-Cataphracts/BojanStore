import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { ReportRangePicker } from '@/components/ReportRangePicker';
import { getOrderStatusCounts } from '@/lib/api/reports';
import { orderStatusMeta } from '@/lib/status';
import type { AdminOrderStatus } from '@/lib/types';

export const metadata: Metadata = { title: 'گزارش سفارش‌ها' };

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

/** Screen 134 - گزارش سفارش‌ها. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const { from, to } = rangeToDates(first(params.range));
  const counts = await getOrderStatusCounts({ from, to });
  const byStatus = (Object.keys(orderStatusMeta) as AdminOrderStatus[]).map((status) => ({
    id: status,
    label: orderStatusMeta[status].label,
    count: counts.find((c) => c.status === status)?.count ?? 0,
  }));
  const totalOrders = counts.reduce((sum, c) => sum + c.count, 0);

  const kpis = [
    { label: 'کل سفارش‌ها', value: toPersianDigits(totalOrders), icon: 'receipt_long' },
    { label: 'تحویل‌شده', value: toPersianDigits(byStatus.find((s) => s.id === 'delivered')?.count ?? 0), icon: 'check_circle' },
    { label: 'لغوشده', value: toPersianDigits(byStatus.find((s) => s.id === 'cancelled')?.count ?? 0), icon: 'cancel' },
    { label: 'مرجوعی', value: toPersianDigits(byStatus.find((s) => s.id === 'returned')?.count ?? 0), icon: 'assignment_return' },
  ];

  const columns = [
    { key: 'label', header: 'وضعیت', cell: (row: (typeof byStatus)[number]) => row.label },
    { key: 'count', header: 'تعداد', cell: (row: (typeof byStatus)[number]) => <span className="tabular">{toPersianDigits(row.count)}</span> },
  ];

  const rows = byStatus;

  return (
    <AdminPage
      title="گزارش سفارش‌ها"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'گزارش‌ها' }, { label: 'سفارش‌ها' }]}
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
