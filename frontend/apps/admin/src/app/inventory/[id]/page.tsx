import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { Badge, Card, Code, Icon, buttonClasses, formatDate, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { getProduct } from '@/lib/api/products';
import { getStockMovements } from '@/lib/api/inventory';
import { LOW_STOCK_THRESHOLD } from '@/lib/status';

export const metadata: Metadata = { title: 'جزئیات موجودی محصول' };

/** Screen 112 — Inventory detail for one product. */
export default async function InventoryDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const product = await getProduct(id);
  if (!product) notFound();

  // Movements are keyed by SKU, which is what the warehouse actually scans.
  const { items: allMovements } = await getStockMovements({ pageSize: 200 });
  const movements = allMovements.filter((movement) => movement.sku === product.sku);
  const low = product.stock <= LOW_STOCK_THRESHOLD;

  return (
    <AdminPage
      title="جزئیات موجودی محصول"
      breadcrumbs={[{ label: 'موجودی و انبار', href: '/inventory' }, { label: product.title }]}
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
      <section className="grid gap-md sm:grid-cols-2 xl:grid-cols-4">
        {[
          {
            label: 'موجودی فعلی',
            value: toPersianDigits(product.stock),
            icon: 'inventory_2',
            warn: low,
          },
          { label: 'رزرو شده', value: toPersianDigits(3), icon: 'lock_clock', warn: false },
          {
            label: 'قابل فروش',
            value: toPersianDigits(Math.max(0, product.stock - 3)),
            icon: 'shopping_cart',
            warn: false,
          },
          { label: 'نقطه سفارش', value: toPersianDigits(LOW_STOCK_THRESHOLD), icon: 'flag', warn: false },
        ].map((kpi) => (
          <Card key={kpi.label} className="flex flex-col gap-sm p-lg">
            <span className="flex items-center justify-between">
              <span className="flex h-10 w-10 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
                <Icon name={kpi.icon} size={22} />
              </span>
              {kpi.warn && <Badge tone="warning">کمبود موجودی</Badge>}
            </span>
            <span className="text-caption text-on-surface-variant">{kpi.label}</span>
            <span className="tabular text-kpi text-primary">{kpi.value}</span>
          </Card>
        ))}
      </section>

      <Card className="flex flex-wrap items-center justify-between gap-md p-lg">
        <div className="flex min-w-0 flex-col gap-xs">
          <span className="text-caption text-on-surface-variant">محصول</span>
          <span className="text-body-lg font-semibold text-primary">{product.title}</span>
          <Code className="text-caption text-on-surface-variant">{product.sku}</Code>
        </div>
        <div className="flex flex-col items-end gap-xs">
          <span className="text-caption text-on-surface-variant">قیمت فروش</span>
          <span className="tabular text-body-lg font-semibold text-primary">
            {formatPrice(product.price)}
          </span>
        </div>
      </Card>

      <section className="flex flex-col gap-md">
        <h3 className="font-headline text-card-title text-primary">تاریخچه حرکات انبار</h3>

        <DataTable
          rows={movements}
          rowKey={(row) => row.id}
          emptyTitle="حرکتی ثبت نشده"
          emptyDescription="ورود و خروج کالا برای این محصول هنوز ثبت نشده است."
          columns={[
            {
              key: 'kind',
              header: 'نوع',
              cell: (row) =>
                row.kind === 'in' ? (
                  <Badge tone="mint">ورود</Badge>
                ) : row.kind === 'out' ? (
                  <Badge tone="warning">خروج</Badge>
                ) : (
                  <Badge tone="neutral">اصلاح</Badge>
                ),
            },
            {
              key: 'quantity',
              header: 'تعداد',
              cell: (row) => (
                <span className="tabular">
                  {row.kind === 'out' ? '−' : '+'}
                  {toPersianDigits(Math.abs(row.quantity))}
                </span>
              ),
            },
            { key: 'reason', header: 'دلیل', cell: (row) => row.reason },
            {
              key: 'date',
              header: 'تاریخ',
              cell: (row) => <span className="tabular">{formatDate(row.at, 'long')}</span>,
            },
            { key: 'by', header: 'کاربر', cell: (row) => row.by },
          ]}
        />
      </section>
    </AdminPage>
  );
}
