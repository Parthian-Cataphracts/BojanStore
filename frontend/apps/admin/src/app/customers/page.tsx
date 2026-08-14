import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Code, formatDate, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getAccounts } from '@/lib/api/customers';
import { requireRole } from '@/lib/auth/server';
import type { AdminAccountDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت کاربران' };
export const dynamic = 'force-dynamic';

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const PAGE_SIZE = 20;

/** The one word that says what an account is. */
const roleMeta: Record<AdminAccountDto['role'], { label: string; tone: 'neutral' | 'teal' | 'mint' | 'coral' }> = {
  customer: { label: 'مشتری', tone: 'neutral' },
  owner: { label: 'مالک', tone: 'coral' },
  product: { label: 'مدیر محصول', tone: 'teal' },
  sales: { label: 'فروش سازمانی', tone: 'teal' },
  support: { label: 'پشتیبانی', tone: 'mint' },
};

const statusMeta: Record<AdminAccountDto['status'], { label: string; tone: 'mint' | 'error' }> = {
  active: { label: 'فعال', tone: 'mint' },
  blocked: { label: 'مسدود', tone: 'error' },
  suspended: { label: 'معلق', tone: 'error' },
};

const columns: Column<AdminAccountDto>[] = [
  {
    key: 'name',
    header: 'نام',
    cell: (row) => (
      <span className="flex flex-wrap items-center gap-xs">
        {row.name || '—'}
        {/* An operator who also shops here is one person, and the list says so
            rather than showing them twice with nothing to connect the rows. */}
        {row.linkedCustomerId && <Badge tone="mint">خریدار هم هست</Badge>}
      </span>
    ),
  },
  {
    key: 'role',
    header: 'نقش',
    cell: (row) => <Badge tone={roleMeta[row.role].tone}>{roleMeta[row.role].label}</Badge>,
  },
  {
    key: 'code',
    header: 'شناسه',
    cell: (row) => (row.code ? <Code className="text-caption">{row.code}</Code> : '—'),
  },
  {
    key: 'phone',
    header: 'موبایل',
    cell: (row) => <span className="tabular">{row.phone ? toPersianDigits(row.phone) : '—'}</span>,
  },
  {
    key: 'email',
    header: 'ایمیل',
    cell: (row) => (row.email ? <Code className="text-caption">{row.email}</Code> : '—'),
  },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (row) => <Badge tone={statusMeta[row.status].tone}>{statusMeta[row.status].label}</Badge>,
  },
  {
    key: 'joined',
    header: 'عضویت',
    cell: (row) => <span className="tabular">{formatDate(row.joinedAt)}</span>,
  },
];

/**
 * Screen 116 — مدیریت کاربران.
 *
 * Shoppers and operators in one table. They were two screens, which made "who
 * has an account here" a question with two answers and no way to tell that the
 * operator on one list and the customer on the other were the same person. The
 * tables stay separate — one is named by orders and the other by audit entries,
 * and neither set of references moves without a migration — but the list does
 * not have to be.
 *
 * Each row opens the editor for its own kind: a shopper's record, or the
 * operator screen. Appointing an operator is still that screen's own job.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  // The same three roles the API admits. Without it the catalogue operator got
  // this far and met an empty table instead of a reason.
  await requireRole('owner', 'sales', 'support');
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const role = first(params.role);
  const status = first(params.status);
  const page = Math.max(1, Number(first(params.page) ?? 1) || 1);

  const { items: rows, total } = await getAccounts({
    q: query,
    role,
    status,
    page,
    pageSize: PAGE_SIZE,
  });

  return (
    <AdminPage
      title="مدیریت کاربران"
      description="مشتری‌ها و اپراتورها کنار هم. هر ردیف در ویرایشگر خودش باز می‌شود."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'کاربران' }]}
    >
      <Suspense fallback={null}>
        <FilterBar
          searchPlaceholder="جستجوی نام، موبایل، ایمیل یا شناسه..."
          filters={[
            {
              param: 'role',
              label: 'نقش',
              options: (Object.keys(roleMeta) as AdminAccountDto['role'][]).map((value) => ({
                value,
                label: roleMeta[value].label,
              })),
            },
            {
              param: 'status',
              label: 'وضعیت',
              options: [
                { value: 'active', label: 'فعال' },
                { value: 'blocked', label: 'مسدود یا معلق' },
              ],
            },
          ]}
        />
      </Suspense>

      <p className="text-caption text-on-surface-variant">{toPersianDigits(total)} حساب</p>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => `${row.kind}-${row.id}`}
        pagination={{ page, pageSize: PAGE_SIZE, total, params, basePath: '/customers' }}
        emptyTitle="حسابی یافت نشد"
        emptyIcon="group"
        actions={(row) => (
          <Link
            href={row.kind === 'operator' ? '/settings/users' : `/customers/${row.id}`}
            className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
          >
            ویرایش
          </Link>
        )}
      />
    </AdminPage>
  );
}
