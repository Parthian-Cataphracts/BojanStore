import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge, Icon, buttonClasses, formatDate } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable } from '@/components/DataTable';
import { getContent } from '@/lib/api/content';
import { contentStatusMeta } from '@/lib/status';
import type { ContentEntry } from '@/lib/types';

export const metadata: Metadata = { title: 'مدیریت بنرها و اسلایدرها' };
export const dynamic = 'force-dynamic';

/** Screen 124 — مدیریت بنرها و اسلایدرها. */
export default async function Page() {
  const { items: rows } = await getContent({ kind: 'banner', pageSize: 100 });

  return (
    <AdminPage
      title="مدیریت بنرها و اسلایدرها"
      breadcrumbs={[{ label: 'محتوا', href: '/content' }, { label: 'مدیریت بنرها و اسلایدرها' }]}
      actions={
        <Link href="/content/banners/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          افزودن
        </Link>
      }
    >
      <DataTable
        rows={rows}
        rowKey={(row) => row.id}
        emptyIcon="image"
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
            href={`/content/banners/${row.id}`}
            className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
          >
            ویرایش
          </Link>
        )}
      />
    </AdminPage>
  );
}
