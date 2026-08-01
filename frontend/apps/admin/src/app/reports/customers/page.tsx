import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { ReportRangePicker } from '@/components/ReportRangePicker';
import { getCustomers } from '@/lib/api/customers';
import { getCustomerSummary } from '@/lib/api/reports';

export const metadata: Metadata = { title: 'گزارش مشتریان' };

/**
 * Screen 136 - گزارش مشتریان.
 *
 * The totals come from the database. They used to be counted over a page of at
 * most 200 customers, so a larger base reported 200 as its size and summed the
 * lifetime spend of only those — a figure that would stop growing once the
 * shop passed the page size.
 */
export default async function Page() {
  const [summary, { items: customers }] = await Promise.all([
    getCustomerSummary(),
    getCustomers({ pageSize: 200 }),
  ]);

  const ranked = [...customers].sort((a, b) => b.totalSpent - a.totalSpent);

  const kpis = [
    { label: 'کل مشتریان', value: toPersianDigits(summary.total), icon: 'group' },
    { label: 'مجموع خرید', value: formatPrice(summary.totalSpend), icon: 'payments' },
    { label: 'مشتری سازمانی', value: toPersianDigits(summary.business), icon: 'business_center' },
    { label: 'مسدود', value: toPersianDigits(summary.blocked), icon: 'block' },
  ];

  const columns = [
    { key: 'name', header: 'مشتری', cell: (row: (typeof ranked)[number]) => row.name },
    { key: 'orders', header: 'سفارش', cell: (row: (typeof ranked)[number]) => <span className="tabular">{toPersianDigits(row.orderCount)}</span> },
    { key: 'spent', header: 'مجموع خرید', cell: (row: (typeof ranked)[number]) => <span className="tabular">{formatPrice(row.totalSpent)}</span> },
  ];

  const rows = ranked;

  return (
    <AdminPage
      title="گزارش مشتریان"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'گزارش‌ها' }, { label: 'مشتریان' }]}
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
