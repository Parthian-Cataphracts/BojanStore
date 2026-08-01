import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Code, Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getProducts } from '@/lib/api/products';
import { productStatusMeta } from '@/lib/status';
import type { AdminProductDto } from '@/lib/api/types';
import type { AdminProduct } from '@/lib/types';

export const metadata: Metadata = { title: 'مدیریت محصولات' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const PAGE_SIZE = 20;

const columns: Column<AdminProductDto>[] = [
  { key: 'title', header: 'نام محصول', cell: (row) => row.title },
  { key: 'sku', header: 'کد کالا', cell: (row) => <Code className="text-caption">{row.sku}</Code> },
  { key: 'brand', header: 'برند', cell: (row) => row.brand },
  { key: 'price', header: 'قیمت', cell: (row) => <span className="tabular">{formatPrice(row.price)}</span> },
  { key: 'stock', header: 'موجودی', cell: (row) => <span className="tabular">{toPersianDigits(row.stock)}</span> },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (row) => {
      const status = row.status as AdminProduct['status'];
      return <Badge tone={productStatusMeta[status].tone}>{productStatusMeta[status].label}</Badge>;
    },
  },
];

/** Screen 96 - مدیریت محصولات. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const status = first(params.status);
  const page = Math.max(1, Number(first(params.page) ?? 1) || 1);

  const { items: rows, total } = await getProducts({ status, q: query, page, pageSize: PAGE_SIZE });

  return (
    <AdminPage
      title="مدیریت محصولات"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'محصولات' }]}
      actions={
        <Link href="/products/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          افزودن محصول
        </Link>
      }
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی نام یا کد محصول..."
          filters={[
            {
              param: 'status',
              label: 'وضعیت',
              options: [
                { value: 'published', label: 'منتشر شده' },
                { value: 'draft', label: 'پیش‌نویس' },
                { value: 'archived', label: 'بایگانی' },
              ],
            },
          ]} />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        pagination={{
          page,
          pageSize: PAGE_SIZE,
          total,
          params,
          basePath: '/products',
        }}
        emptyTitle="محصولی یافت نشد"
        emptyIcon="inventory_2"
        actions={(row) => (
          <Link href={`/products/${row.id}`} className={buttonClasses({ variant: 'outline', size: 'sm' })}>
            ویرایش
          </Link>
        )}
      />
    </AdminPage>
  );
}
