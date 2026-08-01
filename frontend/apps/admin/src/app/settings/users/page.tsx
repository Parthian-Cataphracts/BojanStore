import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Badge, Code, Icon, buttonClasses, formatDate } from '@bojan/ui';
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
        <Link href="/settings/users/new" className={buttonClasses({ size: 'sm', className: 'gap-xs' })}>
          <Icon name="add" size={18} />
          افزودن کاربر
        </Link>
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
