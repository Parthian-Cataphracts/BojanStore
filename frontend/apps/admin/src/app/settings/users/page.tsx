import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Badge, Button, Code, formatDate, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getAdminUsers } from '@/lib/api/settings';
import type { AdminUserDto } from '@/lib/api/types';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'مدیریت کاربران ادمین' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const PAGE_SIZE = 20;

/**
 * The API sends the role as the enum's own name — `owner`, `product`, `sales`,
 * `support` — so the column was printing English into a Persian table. The
 * fixtures happen to carry Persian already, which is why nothing looked wrong
 * in mock mode; anything unrecognised falls through unchanged so both keep
 * working.
 */
const roleLabels: Record<string, string> = {
  owner: 'مالک',
  product: 'مدیر محصول',
  sales: 'فروش سازمانی',
  support: 'پشتیبانی',
};

const roleLabel = (role: string) => roleLabels[role] ?? role;

const columns: Column<AdminUserDto>[] = [
  { key: 'name', header: 'نام', cell: (row) => row.name },
  { key: 'email', header: 'ایمیل', cell: (row) => <Code className="text-caption">{row.email}</Code> },
  { key: 'role', header: 'نقش', cell: (row) => <Badge tone="neutral">{roleLabel(row.role)}</Badge> },
  {
    key: 'active',
    header: 'آخرین فعالیت',
    cell: (row) => <span className="tabular">{row.lastActiveAt ? formatDate(row.lastActiveAt) : '—'}</span>,
  },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (row) =>
      row.status === 'active' ? <Badge tone="mint">فعال</Badge> : <Badge tone="error">معلق</Badge>,
  },
];

/** Screen 145 - مدیریت کاربران ادمین. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRole('owner');
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const role = first(params.role);
  const status = first(params.status);
  const page = Math.max(1, Number(first(params.page) ?? 1) || 1);

  /*
    Was fetching a fixed `pageSize: 100` with no pager under it, so a shop that
    ever grew past a hundred operators silently stopped showing the rest — and
    every visit pulled a hundred rows to render however many there were. The
    backend has taken `page`/`pageSize` all along; only this screen was not
    asking.
  */
  const { items: fetched, total } = await getAdminUsers({ q: query, page, pageSize: PAGE_SIZE });

  /*
    Role and status narrow the fetched page rather than the query: the API's
    operator list filters on the search term only. The same compromise the
    customers screen makes, and it holds here for the same reason — this is the
    staff list, a page of which is the whole of it in any real shop.
  */
  const rows = fetched.filter(
    (user) => (!role || user.role === role) && (!status || user.status === status),
  );

  return (
    <AdminPage
      title="مدیریت کاربران ادمین"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تنظیمات', href: '/settings' }, { label: 'کاربران ادمین' }]}
      actions={
        /*
          Was a link to /settings/users/new, which is not a route — the button
          went to a 404. Nor could the page have been written: `/settings/users`
          is a GET and the API has no endpoint that creates an operator, so
          there is nothing for a form to post to. Creating one is what the
          seeder's owner account and `Seed:AdminPassword` are for.

          Disabled rather than removed: the screen is otherwise the right home
          for it, and a control that says why it cannot be used is more useful
          to an operator looking for the feature than a blank toolbar.
        */
        <Button
          size="sm"
          icon="add"
          disabled
          className="gap-xs"
          hint="افزودن کاربر ادمین هنوز در سرور پیاده‌سازی نشده است."
        >
          افزودن کاربر
        </Button>
      }
    >
      <Suspense fallback={null}>
        <FilterBar
          searchPlaceholder="جستجوی نام یا ایمیل..."
          filters={[
            {
              param: 'role',
              label: 'نقش',
              options: Object.entries(roleLabels).map(([value, label]) => ({ value, label })),
            },
            {
              param: 'status',
              label: 'وضعیت',
              options: [
                { value: 'active', label: 'فعال' },
                { value: 'suspended', label: 'معلق' },
              ],
            },
          ]}
        />
      </Suspense>

      <p className="text-caption text-on-surface-variant">
        {toPersianDigits(total)} کاربر ادمین
      </p>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        pagination={{ page, pageSize: PAGE_SIZE, total, params, basePath: '/settings/users' }}
        emptyTitle="کاربری یافت نشد"
        emptyIcon="admin_panel_settings"
      />
    </AdminPage>
  );
}
