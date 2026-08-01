import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { ReportRangePicker } from '@/components/ReportRangePicker';
import { getFinancialTotals } from '@/lib/api/reports';

export const metadata: Metadata = { title: 'گزارش مالی و پرداخت‌ها' };

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

/** Screen 139 - گزارش مالی و پرداخت‌ها. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const { from, to } = rangeToDates(first(params.range));
  // One call now: the order list was only ever fetched to build the table
  // below, which the API aggregates properly.
  const totals = await getFinancialTotals({ from, to });

  // Aggregated by the API over the same orders the totals cover. This used to
  // be built here by filtering a capped page of orders against two hard-coded
  // method names — cash on delivery is a third, so those orders were missing
  // from the table while still counted in the net revenue printed above it, and
  // a store with more than 200 orders in the range lost the rest as well.
  const byMethod = (totals.byPaymentMethod ?? []).map((row) => ({ id: row.method, ...row }));

  const kpis = [
    { label: 'درآمد ناخالص', value: formatPrice(totals.grossRevenue), icon: 'payments' },
    { label: 'تخفیف‌ها', value: formatPrice(totals.discounts), icon: 'receipt_long' },
    { label: 'خالص', value: formatPrice(totals.netRevenue), icon: 'account_balance' },
    { label: 'تراکنش موفق', value: toPersianDigits(totals.orderCount), icon: 'check_circle' },
  ];

  const columns = [
    { key: 'method', header: 'روش پرداخت', cell: (row: (typeof byMethod)[number]) => row.method },
    { key: 'count', header: 'تعداد', cell: (row: (typeof byMethod)[number]) => <span className="tabular">{toPersianDigits(row.count)}</span> },
    { key: 'amount', header: 'مبلغ', cell: (row: (typeof byMethod)[number]) => <span className="tabular">{formatPrice(row.amount)}</span> },
  ];

  const rows = byMethod;

  return (
    <AdminPage
      title="گزارش مالی و پرداخت‌ها"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'گزارش‌ها' }, { label: 'مالی' }]}
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
