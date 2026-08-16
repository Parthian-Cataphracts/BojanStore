import type { Metadata } from 'next';
import Link from 'next/link';
import { Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { KpiRow } from '@/components/KpiRow';
import { getProducts } from '@/lib/api/products';
import { getStockLevels } from '@/lib/api/reports';

export const metadata: Metadata = { title: 'گزارش موجودی' };

/**
 * Screen 137 - گزارش موجودی.
 *
 * The four figures at the top are counted in the database. They used to be
 * summed from a page of at most 200 products, so a larger catalogue reported
 * the units and the stock value of whatever happened to fit — presented as the
 * whole warehouse. The table below is still a page, but it is a ranked list of
 * the emptiest shelves and reads as one.
 */
export default async function Page() {
  const [levels, { items: products }] = await Promise.all([
    getStockLevels(),
    getProducts({ pageSize: 200 }),
  ]);

  const ranked = [...products].sort((a, b) => a.stock - b.stock);

  const kpis = [
    { label: 'مجموع موجودی', value: toPersianDigits(levels.totalUnits), icon: 'warehouse' },
    { label: 'ارزش انبار', value: formatPrice(levels.inventoryValue), icon: 'payments' },
    { label: 'رو به اتمام', value: toPersianDigits(levels.lowStock), icon: 'warning' },
    { label: 'ناموجود', value: toPersianDigits(levels.outOfStock), icon: 'block' },
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
      {/*
        No range picker here. Stock is a level, not a series: the figures below
        are what is on the shelves now, and a date range cannot ask a different
        question of them. Stock *movements* are the range-able view, and they
        have their own screen.
      */}
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
