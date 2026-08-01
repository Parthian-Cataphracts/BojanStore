import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Icon, buttonClasses, formatDate } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getContent } from '@/lib/api/content';
import { contentStatusMeta } from '@/lib/status';
import type { ContentEntryDto } from '@/lib/api/types';
import type { ContentEntry } from '@/lib/types';

export const metadata: Metadata = { title: 'مدیریت محتوا' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const typeLabels: Record<ContentEntry['type'], string> = {
  article: 'مقاله',
  page: 'صفحه ثابت',
  banner: 'بنر',
  faq: 'سوال متداول',
};

const columns: Column<ContentEntryDto>[] = [
  { key: 'title', header: 'عنوان', cell: (row) => row.title },
  { key: 'type', header: 'نوع', cell: (row) => typeLabels[row.type as ContentEntry['type']] },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (row) => {
      const status = row.status as ContentEntry['status'];
      return <Badge tone={contentStatusMeta[status].tone}>{contentStatusMeta[status].label}</Badge>;
    },
  },
  { key: 'author', header: 'نویسنده', cell: (row) => row.author },
  { key: 'updated', header: 'آخرین ویرایش', cell: (row) => <span className="tabular">{formatDate(row.updatedAt)}</span> },
];

/** Screen 121 - مدیریت محتوا. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const type = first(params.type);

  const { items: rows } = await getContent({ q: query, kind: type, pageSize: 100 });

  return (
    <AdminPage
      title="مدیریت محتوا"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'محتوا' }]}
      actions={
        <Link href="/content/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          محتوای جدید
        </Link>
      }
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی عنوان..."
          filters={[
            {
              param: 'type',
              label: 'نوع',
              options: [
                { value: 'article', label: 'مقاله' },
                { value: 'page', label: 'صفحه ثابت' },
                { value: 'banner', label: 'بنر' },
                { value: 'faq', label: 'سوال متداول' },
              ],
            },
          ]} />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="محتوایی یافت نشد"
        emptyIcon="article"

      />
    </AdminPage>
  );
}
