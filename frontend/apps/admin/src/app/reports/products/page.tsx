import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { ReportRangePicker } from '@/components/ReportRangePicker';
import { getProducts } from '@/lib/api/products';

export const metadata: Metadata = { title: 'گزارش محصولات' };

/** Screen 135 - گزارش محصولات. */
export default async function Page() {
  const { items: products } = await getProducts({ pageSize: 200 });
  const ranked = [...products].sort((a, b) => b.price - a.price);

  const kpis = [
    { label: 'تعداد محصول', value: toPersianDigits(products.length), icon: 'inventory_2' },
    { label: 'منتشرشده', value: toPersianDigits(products.filter((p) => p.status === 'published').length), icon: 'visibility' },
    { label: 'پیش‌نویس', value: toPersianDigits(products.filter((p) => p.status === 'draft').length), icon: 'edit_note' },
    { label: 'ناموجود', value: toPersianDigits(products.filter((p) => p.stock === 0).length), icon: 'block' },
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
