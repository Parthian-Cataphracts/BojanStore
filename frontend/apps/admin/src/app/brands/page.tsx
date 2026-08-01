import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Code, Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getBrands } from '@/lib/api/brands';
import type { AdminBrandDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت برندها' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const columns: Column<AdminBrandDto>[] = [
  { key: 'name', header: 'نام برند', cell: (row) => row.name },
  { key: 'slug', header: 'نشانی', cell: (row) => <Code className="text-caption">{row.slug}</Code> },
  { key: 'count', header: 'تعداد محصول', cell: (row) => <span className="tabular">{toPersianDigits(row.productCount)}</span> },
];

/** Screen 101 - مدیریت برندها. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();

  const { items: rows } = await getBrands({ q: query || undefined, pageSize: 200 });

  return (
    <AdminPage
      title="مدیریت برندها"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'برندها' }]}
      actions={
        <Link href="/brands/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          افزودن برند
        </Link>
      }
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی برند..." />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="برندی یافت نشد"
        emptyIcon="branding_watermark"
        actions={(row) => (
          <Link href={`/brands/${row.id}`} className={buttonClasses({ variant: 'outline', size: 'sm' })}>
            ویرایش
          </Link>
        )}
      />
    </AdminPage>
  );
}
