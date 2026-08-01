import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, buttonClasses, formatDate, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getSupportThreads } from '@/lib/api/support';
import { threadStatusMeta } from '@/lib/status';
import type { SupportThreadDto } from '@/lib/api/types';
import type { SupportThread } from '@/lib/types';

export const metadata: Metadata = { title: 'مدیریت پیام‌های پشتیبانی' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const columns: Column<SupportThreadDto>[] = [
  { key: 'subject', header: 'موضوع', cell: (row) => row.subject },
  { key: 'customer', header: 'مشتری', cell: (row) => row.customer },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (row) => {
      const status = row.status as SupportThread['status'];
      return <Badge tone={threadStatusMeta[status].tone}>{threadStatusMeta[status].label}</Badge>;
    },
  },
  { key: 'priority', header: 'اولویت', cell: (row) => ({ low: 'کم', normal: 'عادی', high: 'زیاد' })[row.priority] },
  { key: 'messages', header: 'پیام‌ها', cell: (row) => <span className="tabular">{toPersianDigits(row.messageCount)}</span> },
  { key: 'updated', header: 'آخرین بروزرسانی', cell: (row) => <span className="tabular">{formatDate(row.updatedAt)}</span> },
];

/** Screen 130 - مدیریت پیام‌های پشتیبانی. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const status = first(params.status);

  const { items: rows } = await getSupportThreads({ q: query, status, pageSize: 100 });

  return (
    <AdminPage
      title="مدیریت پیام‌های پشتیبانی"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'پشتیبانی' }]}
      actions={
        <Link href="/support/replies" className={buttonClasses({ variant: 'outline', size: 'sm' })}>
          پاسخ‌های آماده
        </Link>
      }
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی موضوع یا مشتری..."
          filters={[
            {
              param: 'status',
              label: 'وضعیت',
              options: [
                { value: 'open', label: 'باز' },
                { value: 'answered', label: 'پاسخ داده شد' },
                { value: 'closed', label: 'بسته شده' },
              ],
            },
          ]} />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="پیامی یافت نشد"
        emptyIcon="forum"
        actions={(row) => (
          <Link href={`/support/${row.id}`} className={buttonClasses({ variant: 'outline', size: 'sm' })}>
            مشاهده گفتگو
          </Link>
        )}
      />
    </AdminPage>
  );
}
