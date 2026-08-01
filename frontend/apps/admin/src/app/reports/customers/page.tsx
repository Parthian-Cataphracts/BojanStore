import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { ReportRangePicker } from '@/components/ReportRangePicker';
import { getCustomers } from '@/lib/api/customers';

export const metadata: Metadata = { title: 'گزارش مشتریان' };

/** Screen 136 - گزارش مشتریان. */
export default async function Page() {
  const { items: customers } = await getCustomers({ pageSize: 200 });
  const ranked = [...customers].sort((a, b) => b.totalSpent - a.totalSpent);
  const spend = ranked.reduce((sum, c) => sum + c.totalSpent, 0);

  const kpis = [
    { label: 'کل مشتریان', value: toPersianDigits(ranked.length), icon: 'group' },
    { label: 'مجموع خرید', value: formatPrice(spend), icon: 'payments' },
    { label: 'مشتری سازمانی', value: toPersianDigits(ranked.filter((c) => c.group === 'سازمانی').length), icon: 'business_center' },
    { label: 'مسدود', value: toPersianDigits(ranked.filter((c) => c.status === 'blocked').length), icon: 'block' },
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
