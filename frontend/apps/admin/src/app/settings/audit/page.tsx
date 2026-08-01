import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Code, formatDate } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getAuditLog } from '@/lib/api/settings';
import type { AuditEntryDto } from '@/lib/api/types';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'تاریخچه فعالیت ادمین‌ها' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const columns: Column<AuditEntryDto>[] = [
  { key: 'actor', header: 'کاربر', cell: (row) => row.actor },
  { key: 'action', header: 'عملیات', cell: (row) => row.action },
  { key: 'target', header: 'هدف', cell: (row) => <Code className="text-caption">{row.target}</Code> },
  { key: 'at', header: 'زمان', cell: (row) => <span className="tabular">{formatDate(row.at, 'long')}</span> },
  { key: 'ip', header: 'IP', cell: (row) => <Code className="text-caption text-on-surface-variant">{row.ip}</Code> },
];

/** Screen 147 - تاریخچه فعالیت ادمین‌ها. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRole('owner');
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();

  const { items: rows } = await getAuditLog({ q: query, pageSize: 100 });

  return (
    <AdminPage
      title="تاریخچه فعالیت ادمین‌ها"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تنظیمات', href: '/settings' }, { label: 'تاریخچه فعالیت' }]}
      
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی کاربر یا عملیات..." />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="رکوردی یافت نشد"
        emptyIcon="history"

      />
    </AdminPage>
  );
}
