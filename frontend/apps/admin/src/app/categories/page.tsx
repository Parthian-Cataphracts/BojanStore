import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Code, Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getCategories } from '@/lib/api/categories';
import type { AdminCategoryDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت دسته‌بندی‌ها' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const columns: Column<AdminCategoryDto>[] = [
  { key: 'name', header: 'نام', cell: (row) => row.name },
  { key: 'slug', header: 'نشانی', cell: (row) => <Code className="text-caption">{row.slug}</Code> },
  { key: 'parent', header: 'دسته والد', cell: (row) => row.parentName ?? '—' },
  { key: 'count', header: 'تعداد محصول', cell: (row) => <span className="tabular">{toPersianDigits(row.productCount)}</span> },
];

/** Screen 99 - مدیریت دسته‌بندی‌ها. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();

  const { items: rows } = await getCategories({ q: query || undefined, pageSize: 200 });

  return (
    <AdminPage
      title="مدیریت دسته‌بندی‌ها"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'دسته‌بندی‌ها' }]}
      actions={
        <Link href="/categories/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          افزودن دسته‌بندی
        </Link>
      }
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی دسته‌بندی..." />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="دسته‌بندی یافت نشد"
        emptyIcon="category"
        actions={(row) => (
          <Link href={`/categories/${row.id}`} className={buttonClasses({ variant: 'outline', size: 'sm' })}>
            ویرایش
          </Link>
        )}
      />
    </AdminPage>
  );
}
