import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { ReportRangePicker } from '@/components/ReportRangePicker';
import { getProducts } from '@/lib/api/products';
import { getCatalogueSummary } from '@/lib/api/reports';

export const metadata: Metadata = { title: 'گزارش محصولات' };

/**
 * Screen 135 - گزارش محصولات.
 *
 * The counts are the catalogue's, taken from the database. They used to be
 * `products.length` over a page capped at 200, so any catalogue larger than
 * that reported exactly 200 products and counted its published, draft and
 * out-of-stock totals only among those.
 */
export default async function Page() {
  const [summary, { items: products }] = await Promise.all([
    getCatalogueSummary(),
    getProducts({ pageSize: 200 }),
  ]);

  const ranked = [...products].sort((a, b) => b.price - a.price);

  const kpis = [
    { label: 'تعداد محصول', value: toPersianDigits(summary.total), icon: 'inventory_2' },
    { label: 'منتشرشده', value: toPersianDigits(summary.published), icon: 'visibility' },
    { label: 'پیش‌نویس', value: toPersianDigits(summary.draft), icon: 'edit_note' },
    { label: 'ناموجود', value: toPersianDigits(summary.outOfStock), icon: 'block' },
  ];

  const columns = [
    { key: 'title', header: 'محصول', cell: (row: (typeof ranked)[number]) => row.title },
    { key: 'price', header: 'قیمت', cell: (row: (typeof ranked)[number]) => <span className="tabular">{formatPrice(row.price)}</span> },
    { key: 'stock', header: 'موجودی', cell: (row: (typeof ranked)[number]) => <span className="tabular">{toPersianDigits(row.stock)}</span> },
  ];

  const rows = ranked;

  return (
    <AdminPage
      title="گزارش محصولات"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'گزارش‌ها' }, { label: 'محصولات' }]}
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
