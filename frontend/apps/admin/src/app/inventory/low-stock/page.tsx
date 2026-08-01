import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge, Card, Code, EmptyState, Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { getProducts } from '@/lib/api/products';
import { LOW_STOCK_THRESHOLD } from '@/lib/status';

export const metadata: Metadata = { title: 'هشدار کمبود موجودی' };
// No searchParams to force dynamic rendering, and the data is live — do not
// let `next build` try to prerender this against a backend that may not be up.
export const dynamic = 'force-dynamic';

/** Screen 115 — Low-stock alerts. */
export default async function LowStockPage() {
  const { items: products } = await getProducts({ pageSize: 200 });
  const low = products
    .filter((product) => product.stock <= LOW_STOCK_THRESHOLD)
    .sort((a, b) => a.stock - b.stock);

  const outOfStock = low.filter((product) => product.stock === 0);

  return (
    <AdminPage
      title="هشدار کمبود موجودی"
      description={`محصولاتی که موجودی آن‌ها به ${toPersianDigits(LOW_STOCK_THRESHOLD)} عدد یا کمتر رسیده است.`}
      breadcrumbs={[{ label: 'موجودی و انبار', href: '/inventory' }, { label: 'هشدار کمبود' }]}
      actions={
        <Link href="/inventory/receive" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          ثبت ورود کالا
        </Link>
      }
    >
      <section className="grid gap-md sm:grid-cols-3">
        {[
          { label: 'کالاهای رو به اتمام', value: low.length - outOfStock.length, tone: 'warning' as const },
          { label: 'کالاهای ناموجود', value: outOfStock.length, tone: 'error' as const },
          { label: 'مجموع نیازمند بررسی', value: low.length, tone: 'neutral' as const },
        ].map((kpi) => (
          <Card key={kpi.label} className="flex flex-col gap-sm p-lg">
            <span className="text-caption text-on-surface-variant">{kpi.label}</span>
            <span className="tabular text-kpi-mobile text-primary md:text-kpi">
              {toPersianDigits(kpi.value)}
            </span>
          </Card>
        ))}
      </section>

      {low.length > 0 ? (
        <DataTable
          rows={low}
          rowKey={(row) => row.id}
          columns={[
            { key: 'title', header: 'محصول', cell: (row) => row.title },
            { key: 'sku', header: 'SKU', cell: (row) => <Code>{row.sku}</Code> },
            {
              key: 'stock',
              header: 'موجودی',
              cell: (row) =>
                row.stock === 0 ? (
                  <Badge tone="error">ناموجود</Badge>
                ) : (
                  <Badge tone="warning">{toPersianDigits(row.stock)} عدد</Badge>
                ),
            },
            { key: 'price', header: 'قیمت', cell: (row) => formatPrice(row.price) },
          ]}
          actions={(row) => (
            <Link
              href={`/inventory/${row.id}`}
              className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
            >
              جزئیات
            </Link>
          )}
        />
      ) : (
        <Card surface="plain" className="p-lg">
          <EmptyState
            icon="check_circle"
            title="همه‌چیز مرتب است"
            description="هیچ کالایی زیر نقطه سفارش نیست."
          />
        </Card>
      )}
    </AdminPage>
  );
}
