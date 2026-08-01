import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Code, Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getCollections } from '@/lib/api/collections';
import type { AdminCollectionDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت کالکشن‌ها' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const columns: Column<AdminCollectionDto>[] = [
  { key: 'name', header: 'نام کالکشن', cell: (row) => row.title },
  { key: 'slug', header: 'نشانی', cell: (row) => <Code className="text-caption">{row.slug}</Code> },
  { key: 'count', header: 'تعداد محصول', cell: (row) => <span className="tabular">{toPersianDigits(row.productCount)}</span> },
  { key: 'featured', header: 'ویژه', cell: (row) => row.featured ? <Badge tone="mint">ویژه</Badge> : <span className="text-outline">—</span> },
];

/** Screen 103 - مدیریت کالکشن‌ها. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();

  const { items: rows } = await getCollections({ q: query || undefined, pageSize: 200 });

  return (
    <AdminPage
      title="مدیریت کالکشن‌ها"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'کالکشن‌ها' }]}
      actions={
        <Link href="/collections/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          افزودن کالکشن
        </Link>
      }
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی کالکشن..." />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="کالکشنی یافت نشد"
        emptyIcon="collections"
        actions={(row) => (
          <Link href={`/collections/${row.id}`} className={buttonClasses({ variant: 'outline', size: 'sm' })}>
            ویرایش
          </Link>
        )}
      />
    </AdminPage>
  );
}
