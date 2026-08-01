import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Card, Code, Icon, buttonClasses, formatDate, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { KpiRow } from '@/components/KpiRow';
import { getInventory, getStockMovements } from '@/lib/api/inventory';
import { LOW_STOCK_THRESHOLD } from '@/lib/status';
import type { InventoryRowDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت موجودی و انبار' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const PAGE_SIZE = 20;

/** Stock level drives the badge; the threshold lives in one place. */
function stockBadge(stock: number) {
  if (stock === 0) return <Badge tone="error">ناموجود</Badge>;
  if (stock <= LOW_STOCK_THRESHOLD) return <Badge tone="warning">رو به اتمام</Badge>;
  return <Badge tone="mint">موجود</Badge>;
}

const columns: Column<InventoryRowDto>[] = [
  { key: 'title', header: 'کالا', cell: (row) => row.title },
  { key: 'sku', header: 'کد کالا', cell: (row) => <Code className="text-caption">{row.sku}</Code> },
  {
    key: 'stock',
    header: 'موجودی',
    cell: (row) => <span className="tabular">{toPersianDigits(row.stock)}</span>,
  },
  { key: 'state', header: 'وضعیت', cell: (row) => stockBadge(row.stock) },
  {
    key: 'updated',
    header: 'آخرین تغییر',
    cell: (row) => <span className="tabular">{formatDate(row.updatedAt)}</span>,
  },
];

/** Screens 111 and 115 — Inventory with the low-stock alert. */
export default async function InventoryPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const level = first(params.level);
  const page = Math.max(1, Number(first(params.page) ?? 1) || 1);

  // Level (out/low/ok) has no backend filter counterpart yet, so the fetched
  // page is filtered client-side, same as the fixture path did.
  const { items: fetched, total: fetchedTotal } = await getInventory({ q: query, pageSize: 200 });
  const matched = fetched.filter((row) => {
    const matchesLevel =
      !level ||
      (level === 'out' && row.stock === 0) ||
      (level === 'low' && row.stock > 0 && row.stock <= LOW_STOCK_THRESHOLD) ||
      (level === 'ok' && row.stock > LOW_STOCK_THRESHOLD);
    return matchesLevel;
  });

  const outOfStock = fetched.filter((row) => row.stock === 0);
  const lowStock = fetched.filter((row) => row.stock > 0 && row.stock <= LOW_STOCK_THRESHOLD);

  const rows = matched.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const totalUnits = fetched.reduce((sum, row) => sum + row.stock, 0);
  const { items: movements } = await getStockMovements({ pageSize: 10 });

  return (
    <AdminPage
      title="مدیریت موجودی و انبار"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'موجودی و انبار' }]}
      actions={
        <>
          <Link href="/inventory/receive" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
            <Icon name="add" size={18} />
            ثبت ورود کالا
          </Link>
          <Link
            href="/inventory/adjust"
            className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
          >
            <Icon name="tune" size={18} />
            اصلاح موجودی
          </Link>
        </>
      }
    >
      <KpiRow
        items={[
          { label: 'مجموع موجودی', value: toPersianDigits(totalUnits), icon: 'warehouse' },
          { label: 'رو به اتمام', value: toPersianDigits(lowStock.length), icon: 'warning', delta: 'نیاز به بررسی', up: false },
          { label: 'ناموجود', value: toPersianDigits(outOfStock.length), icon: 'block' },
          { label: 'تعداد کالا', value: toPersianDigits(fetchedTotal), icon: 'inventory_2' },
        ]}
      />

      {/* Screen 115 — the low-stock alert sits above the table when it matters. */}
      {(lowStock.length > 0 || outOfStock.length > 0) && (
        <Card className="flex items-start gap-sm border-secondary-container/50 bg-secondary-fixed/40 p-md">
          <Icon name="warning" size={20} className="mt-px shrink-0 text-secondary" />
          <p className="text-body-md leading-relaxed text-on-secondary-fixed-variant">
            {toPersianDigits(lowStock.length)} کالا رو به اتمام و{' '}
            {toPersianDigits(outOfStock.length)} کالا ناموجود است.{' '}
            <Link href="?level=low" className="font-semibold underline">
              مشاهده فهرست
            </Link>
          </p>
        </Card>
      )}

      <Suspense fallback={null}>
        <FilterBar
          searchPlaceholder="جستجو در انبار..."
          filters={[
            {
              param: 'level',
              label: 'سطح موجودی',
              options: [
                { value: 'ok', label: 'موجود' },
                { value: 'low', label: 'رو به اتمام' },
                { value: 'out', label: 'ناموجود' },
              ],
            },
          ]}
        />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        pagination={{
          page,
          pageSize: PAGE_SIZE,
          total: matched.length,
          params,
          basePath: '/inventory',
        }}
        emptyTitle="کالایی یافت نشد"
        emptyIcon="warehouse"
        actions={(row) => (
          <Link
            href={`/inventory/${row.id}`}
            className={buttonClasses({ variant: 'outline', size: 'sm' })}
          >
            جزئیات
          </Link>
        )}
      />

      {/* Screens 113 and 114 — recent movements. */}
      <section className="flex flex-col gap-md">
        <h3 className="font-headline text-card-title text-primary md:text-section-title">
          آخرین تغییرات موجودی
        </h3>

        <DataTable
          columns={[
            { key: 'product', header: 'کالا', cell: (row) => row.productTitle },
            { key: 'sku', header: 'کد', cell: (row) => <Code className="text-caption">{row.sku}</Code> },
            {
              key: 'kind',
              header: 'نوع',
              cell: (row) => {
                const meta = (
                  {
                    in: { label: 'ورود', tone: 'mint' as const },
                    out: { label: 'خروج', tone: 'neutral' as const },
                    adjust: { label: 'اصلاح', tone: 'warning' as const },
                  } as const
                )[row.kind as 'in' | 'out' | 'adjust'];
                return <Badge tone={meta.tone}>{meta.label}</Badge>;
              },
            },
            {
              key: 'qty',
              header: 'تعداد',
              cell: (row) => (
                <span className="tabular">
                  {row.quantity > 0 ? '+' : '−'}
                  {toPersianDigits(Math.abs(row.quantity))}
                </span>
              ),
            },
            { key: 'reason', header: 'دلیل', cell: (row) => row.reason },
            {
              key: 'at',
              header: 'زمان',
              cell: (row) => <span className="tabular">{formatDate(row.at, 'long')}</span>,
            },
          ]}
          rows={movements}
          rowKey={(row) => row.id}
          emptyTitle="تغییری ثبت نشده"
          emptyIcon="history"
        />
      </section>
    </AdminPage>
  );
}
