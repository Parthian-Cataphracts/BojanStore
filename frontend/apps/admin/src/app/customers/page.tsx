import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, buttonClasses, formatDate, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getCustomers } from '@/lib/api/customers';
import { requireRole } from '@/lib/auth/server';
import type { AdminCustomerDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت مشتریان' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const PAGE_SIZE = 20;

const columns: Column<AdminCustomerDto>[] = [
  { key: 'name', header: 'نام', cell: (row) => row.name },
  { key: 'phone', header: 'موبایل', cell: (row) => <span className="tabular">{toPersianDigits(row.phone)}</span> },
  { key: 'group', header: 'گروه', cell: (row) => <Badge tone="neutral">{row.group}</Badge> },
  { key: 'orders', header: 'سفارش', cell: (row) => <span className="tabular">{toPersianDigits(row.orderCount)}</span> },
  { key: 'spent', header: 'مجموع خرید', cell: (row) => <span className="tabular">{formatPrice(row.totalSpent)}</span> },
  { key: 'joined', header: 'عضویت', cell: (row) => <span className="tabular">{formatDate(row.joinedAt)}</span> },
];

/** Screen 116 - مدیریت مشتریان. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  // The same three roles the API admits. Without it the catalogue operator got
  // this far and met an empty table instead of a reason.
  await requireRole('owner', 'sales', 'support');
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const group = first(params.group);
  const page = Math.max(1, Number(first(params.page) ?? 1) || 1);

  // The backend filters by status, not group; group narrows the fetched page.
  const { items: fetched, total } = await getCustomers({ q: query, page, pageSize: PAGE_SIZE });
  const rows = fetched.filter((row) => !group || row.group === group);

  return (
    <AdminPage
      title="مدیریت مشتریان"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'مشتریان' }]}
      
    >
      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی نام یا شماره موبایل..."
          filters={[
            {
              param: 'group',
              label: 'گروه',
              options: [
                { value: 'عادی', label: 'عادی' },
                { value: 'طلایی', label: 'طلایی' },
                { value: 'سازمانی', label: 'سازمانی' },
              ],
            },
          ]} />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        pagination={{
          page,
          pageSize: PAGE_SIZE,
          total,
          params,
          basePath: '/customers',
        }}
        emptyTitle="مشتری یافت نشد"
        emptyIcon="group"
        actions={(row) => (
          <Link href={`/customers/${row.id}`} className={buttonClasses({ variant: 'outline', size: 'sm' })}>
            جزئیات
          </Link>
        )}
      />
    </AdminPage>
  );
}
