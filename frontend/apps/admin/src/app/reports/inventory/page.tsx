import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { ReportRangePicker } from '@/components/ReportRangePicker';
import { getProducts } from '@/lib/api/products';
import { LOW_STOCK_THRESHOLD } from '@/lib/status';

export const metadata: Metadata = { title: 'گزارش موجودی' };

/** Screen 137 - گزارش موجودی. */
export default async function Page() {
  const { items: products } = await getProducts({ pageSize: 200 });
  const ranked = [...products].sort((a, b) => a.stock - b.stock);
  const units = ranked.reduce((sum, p) => sum + p.stock, 0);
  const value = ranked.reduce((sum, p) => sum + p.stock * p.costPrice, 0);

  const kpis = [
    { label: 'مجموع موجودی', value: toPersianDigits(units), icon: 'warehouse' },
    { label: 'ارزش انبار', value: formatPrice(value), icon: 'payments' },
    { label: 'رو به اتمام', value: toPersianDigits(ranked.filter((p) => p.stock > 0 && p.stock <= LOW_STOCK_THRESHOLD).length), icon: 'warning' },
    { label: 'ناموجود', value: toPersianDigits(ranked.filter((p) => p.stock === 0).length), icon: 'block' },
  ];

  const columns = [
    { key: 'title', header: 'کالا', cell: (row: (typeof ranked)[number]) => row.title },
    { key: 'stock', header: 'موجودی', cell: (row: (typeof ranked)[number]) => <span className="tabular">{toPersianDigits(row.stock)}</span> },
    { key: 'value', header: 'ارزش', cell: (row: (typeof ranked)[number]) => <span className="tabular">{formatPrice(row.stock * row.costPrice)}</span> },
  ];

  const rows = ranked;

  return (
    <AdminPage
      title="گزارش موجودی"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'گزارش‌ها' }, { label: 'موجودی' }]}
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
