import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Code, buttonClasses, formatDate, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getBusinessRequests } from '@/lib/api/business-requests';
import type { AdminBusinessRequestDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت درخواست‌های سازمانی' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const columns: Column<AdminBusinessRequestDto>[] = [
  { key: 'code', header: 'شناسه', cell: (row) => <Code className="font-semibold text-primary">{row.code}</Code> },
  { key: 'org', header: 'سازمان', cell: (row) => row.organization },
  { key: 'contact', header: 'فرد رابط', cell: (row) => row.contact },
  { key: 'items', header: 'تعداد اقلام', cell: (row) => <span className="tabular">{toPersianDigits(row.itemCount)}</span> },
  { key: 'created', header: 'تاریخ', cell: (row) => <span className="tabular">{formatDate(row.createdAt)}</span> },
];

/** Screen 119 - مدیریت درخواست‌های سازمانی. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();

  const { items: rows } = await getBusinessRequests({ q: query, pageSize: 100 });

  return (
    <AdminPage
      title="مدیریت درخواست‌های سازمانی"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'درخواست‌های سازمانی' }]}
      
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی شناسه یا سازمان..." />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="درخواستی یافت نشد"
        emptyIcon="business_center"
        actions={(row) => (
          <Link href={`/business-requests/${row.id}`} className={buttonClasses({ variant: 'outline', size: 'sm' })}>
            جزئیات
          </Link>
        )}
      />
    </AdminPage>
  );
}
