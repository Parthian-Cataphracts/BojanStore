import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge, Icon, buttonClasses, formatDate } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { getContent } from '@/lib/api/content';
import { contentStatusMeta } from '@/lib/status';
import type { ContentEntry } from '@/lib/types';

export const metadata: Metadata = { title: 'مدیریت سوالات متداول' };
export const dynamic = 'force-dynamic';

/** Screen 126 — مدیریت سوالات متداول. */
export default async function Page() {
  const { items: rows } = await getContent({ kind: 'faq', pageSize: 100 });

  return (
    <AdminPage
      title="مدیریت سوالات متداول"
      breadcrumbs={[{ label: 'محتوا', href: '/content' }, { label: 'مدیریت سوالات متداول' }]}
      actions={
        <Link href="/content/faq/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          افزودن
        </Link>
      }
    >
      <DataTable
        rows={rows}
        rowKey={(row) => row.id}
        emptyIcon="help"
        emptyTitle="موردی ثبت نشده"
        emptyDescription="برای شروع، یک مورد جدید اضافه کنید."
        columns={[
          { key: 'title', header: 'عنوان', cell: (row) => row.title },
          {
            key: 'status',
            header: 'وضعیت',
            cell: (row) => {
              const meta = contentStatusMeta[row.status as ContentEntry['status']];
              return <Badge tone={meta.tone}>{meta.label}</Badge>;
            },
          },
          { key: 'author', header: 'نویسنده', cell: (row) => row.author },
          {
            key: 'updated',
            header: 'آخرین ویرایش',
            cell: (row) => <span className="tabular">{formatDate(row.updatedAt, 'long')}</span>,
          },
        ]}
        actions={(row) => (
          <Link
            href={`/content/faq/${row.id}`}
            className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
          >
            ویرایش
          </Link>
        )}
      />
    </AdminPage>
  );
}
