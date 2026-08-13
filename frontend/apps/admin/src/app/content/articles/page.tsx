import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Icon, buttonClasses, formatDate, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getAdminArticles } from '@/lib/api/content';
import type { AdminArticleDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت مقالات مجله' };
export const dynamic = 'force-dynamic';

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const statusMeta: Record<AdminArticleDto['status'], { label: string; tone: 'mint' | 'warning' | 'neutral' }> = {
  published: { label: 'منتشر شده', tone: 'mint' },
  draft: { label: 'پیش‌نویس', tone: 'warning' },
  archived: { label: 'بایگانی', tone: 'neutral' },
};

const columns: Column<AdminArticleDto>[] = [
  {
    key: 'title',
    header: 'عنوان',
    cell: (row) => (
      <span className="flex flex-wrap items-center gap-xs">
        {row.title}
        {row.featured && <Badge tone="teal">ویژه</Badge>}
      </span>
    ),
  },
  { key: 'category', header: 'دسته', cell: (row) => row.category || '—' },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (row) => <Badge tone={statusMeta[row.status].tone}>{statusMeta[row.status].label}</Badge>,
  },
  {
    key: 'reading',
    header: 'زمان خواندن',
    cell: (row) => <span className="tabular">{toPersianDigits(row.readingMinutes)} دقیقه</span>,
  },
  {
    key: 'published',
    header: 'تاریخ انتشار',
    cell: (row) => <span className="tabular">{formatDate(row.publishedAt)}</span>,
  },
];

/**
 * Screen 122 — مدیریت مقالات مجله.
 *
 * Reads `/articles`, which is the table the storefront's magazine reads. It
 * used to read `/content?kind=article` — a different table entirely — so this
 * list showed articles the site did not have and the site showed articles this
 * list did not.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const status = first(params.status);

  const { items: rows, total } = await getAdminArticles({ q: query, status, pageSize: 100 });

  return (
    <AdminPage
      title="مدیریت مقالات مجله"
      breadcrumbs={[{ label: 'محتوا', href: '/content' }, { label: 'مدیریت مقالات مجله' }]}
      actions={
        <Link href="/content/articles/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          افزودن
        </Link>
      }
    >
      <Suspense fallback={null}>
        <FilterBar
          searchPlaceholder="جستجوی عنوان یا نشانی..."
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
          ]}
        />
      </Suspense>

      <p className="text-caption text-on-surface-variant">
        {toPersianDigits(total)} مقاله — منتشرشده‌ها در «مجله» سایت دیده می‌شوند.
      </p>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyIcon="article"
        emptyTitle="مقاله‌ای ثبت نشده"
        emptyDescription="برای شروع، یک مقاله جدید اضافه کنید."
        actions={(row) => (
          <div className="flex items-center gap-md">
            {row.status === 'published' && (
              // Straight to the page a reader sees, which is the only proof
              // that publishing did what it says.
              <a
                href={`/magazine/${row.slug}`}
                target="_blank"
                rel="noreferrer"
                className="text-label-md text-on-surface-variant transition-colors hover:text-primary"
              >
                مشاهده
              </a>
            )}
            <Link
              href={`/content/articles/${row.id}`}
              className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
            >
              ویرایش
            </Link>
          </div>
        )}
      />
    </AdminPage>
  );
}
