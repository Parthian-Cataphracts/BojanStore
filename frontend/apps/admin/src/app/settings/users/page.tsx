import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Badge, Button, Code, formatDate } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getAdminUsers } from '@/lib/api/settings';
import type { AdminUserDto } from '@/lib/api/types';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'مدیریت کاربران ادمین' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const columns: Column<AdminUserDto>[] = [
  { key: 'name', header: 'نام', cell: (row) => row.name },
  { key: 'email', header: 'ایمیل', cell: (row) => <Code className="text-caption">{row.email}</Code> },
  { key: 'role', header: 'نقش', cell: (row) => <Badge tone="neutral">{row.role}</Badge> },
  { key: 'active', header: 'آخرین فعالیت', cell: (row) => <span className="tabular">{row.lastActiveAt ? formatDate(row.lastActiveAt) : '—'}</span> },
  { key: 'status', header: 'وضعیت', cell: (row) => row.status === 'active' ? <Badge tone="mint">فعال</Badge> : <Badge tone="error">معلق</Badge> },
];

/** Screen 145 - مدیریت کاربران ادمین. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRole('owner');
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();

  const { items: rows } = await getAdminUsers({ q: query, pageSize: 100 });

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
        <FilterBar searchPlaceholder="جستجوی نام یا ایمیل..." />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyTitle="کاربری یافت نشد"
        emptyIcon="admin_panel_settings"

      />
    </AdminPage>
  );
}
