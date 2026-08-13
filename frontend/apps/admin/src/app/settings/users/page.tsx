import type { Metadata } from 'next';
import { Suspense } from 'react';
import { toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { AdminUserManager } from '@/components/AdminUserManager';
import { FilterBar } from '@/components/FilterBar';
import { getAdminUsers } from '@/lib/api/settings';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'مدیریت کاربران ادمین' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const PAGE_SIZE = 20;

/**
 * The filter's own labels.
 *
 * The API sends the role as the enum's own name — `owner`, `product`, `sales`,
 * `support` — and the filter has to post those exact keys back. What the table
 * draws is `AdminUserManager`'s business, which holds the same four beside the
 * sentence describing each; this is only what the dropdown says.
 */
const roleLabels: Record<string, string> = {
  owner: 'مالک',
  product: 'مدیر محصول',
  sales: 'فروش سازمانی',
  support: 'پشتیبانی',
};

/** Screen 145 - مدیریت کاربران ادمین. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const session = await requireRole('owner');
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
    >
      <Suspense fallback={null}>
        <FilterBar
          searchPlaceholder="جستجوی نام، ایمیل یا شماره..."
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

      {/*
        The signed-in operator's own id travels down so their row can explain
        why half of it is off rather than simply refusing: nobody demotes,
        suspends or resets themselves from here — the first two lock the panel
        behind a colleague's availability, and the last two have self-service
        screens that ask for the current credential first, which is the whole
        thing standing between a stolen session and the account under it.
      */}
      <AdminUserManager
        users={rows}
        currentUserId={session.sub}
        pagination={{ page, pageSize: PAGE_SIZE, total, params, basePath: '/settings/users' }}
      />
    </AdminPage>
  );
}
