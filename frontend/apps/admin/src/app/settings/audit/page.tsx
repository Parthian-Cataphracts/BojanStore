import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Badge, Code, formatDate, toPersianDigits, type BadgeTone } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { AuditDateRange } from '@/components/AuditDateRange';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getAuditLog } from '@/lib/api/settings';
import { instantFor } from '@/lib/day-range';
import type { AuditEntryDto } from '@/lib/api/types';
import { requireRole } from '@/lib/auth/server';

export const metadata: Metadata = { title: 'تاریخچه فعالیت ادمین‌ها' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const PAGE_SIZE = 20;

/**
 * The kind of change a row records, read off the end of its action key.
 *
 * Three, not the create/update/delete a reader might expect, because three is
 * what this application writes: an upsert is audited as `product.saved`, and
 * the row genuinely does not know whether that save made the product or edited
 * it. Offering a «ایجاد» filter would be offering one that matches nothing.
 *
 * The same rule runs in SQL for the filter — see `ListAuditAsync`. Kept in step
 * by both being one line about a suffix rather than a table of every key.
 */
const KINDS = {
  saved: { label: 'ثبت یا ویرایش', tone: 'warning' as BadgeTone },
  deleted: { label: 'حذف', tone: 'error' as BadgeTone },
  other: { label: 'سایر', tone: 'neutral' as BadgeTone },
};

type Kind = keyof typeof KINDS;

function kindOf(action: string): Kind {
  if (action.endsWith('.deleted')) return 'deleted';
  if (action.endsWith('.saved') || action.endsWith('.updated')) return 'saved';
  return 'other';
}

/**
 * What the first segment of an action key is, in Persian.
 *
 * Best-effort: a key this does not know is printed as it is, which is a stable
 * English word an operator can still search for. The alternative — hiding the
 * segment when there is no translation — would make a new audit source look
 * like a row with no subject.
 */
const SUBJECTS: Record<string, string> = {
  admin: 'حساب اپراتور',
  article: 'مقاله',
  backup: 'پشتیبان‌گیری',
  brand: 'برند',
  campaign: 'کمپین',
  category: 'دسته‌بندی',
  collection: 'کالکشن',
  content: 'محتوا',
  coupon: 'کد تخفیف',
  customer: 'مشتری',
  database: 'پایگاه‌داده',
  inventory: 'موجودی',
  loyalty: 'باشگاه مشتریان',
  mailbox: 'صندوق پستی',
  notification: 'اعلان',
  order: 'سفارش',
  payment: 'پرداخت',
  product: 'محصول',
  push: 'اعلان مرورگر',
  quote: 'پیش‌فاکتور',
  report: 'گزارش',
  return: 'مرجوعی',
  roles: 'نقش‌ها',
  settings: 'تنظیمات',
  shipping: 'ارسال',
  sms: 'پیامک',
  support: 'پشتیبانی',
  wallet: 'کیف پول',
};

function subjectOf(action: string): string {
  const head = action.split('.')[0] ?? action;
  return SUBJECTS[head] ?? head;
}

const columns: Column<AuditEntryDto>[] = [
  {
    key: 'action',
    header: 'فعالیت',
    cell: (row) => {
      const kind = KINDS[kindOf(row.action)];
      return (
        <div className="gap-sm flex flex-wrap items-center">
          <Badge tone={kind.tone}>{kind.label}</Badge>
          <span className="text-on-surface font-medium">{subjectOf(row.action)}</span>
          {/* The key itself, because the badge is a bucket and this is the
              fact. It is also what the search box matches on. */}
          <Code className="text-helper text-on-surface-variant">{row.action}</Code>
        </div>
      );
    },
  },
  { key: 'actor', header: 'کاربر', cell: (row) => row.actor },
  {
    key: 'target',
    header: 'هدف',
    cell: (row) => <Code className="text-caption">{row.target}</Code>,
  },
  {
    key: 'at',
    header: 'زمان',
    cell: (row) => <span className="tabular whitespace-nowrap">{formatDate(row.at, 'long')}</span>,
  },
  {
    key: 'ip',
    header: 'IP',
    cell: (row) =>
      row.ip ? (
        <Code className="text-caption text-on-surface-variant">{row.ip}</Code>
      ) : (
        <span className="text-outline">—</span>
      ),
  },
];

const KIND_OPTIONS = (Object.keys(KINDS) as Kind[]).map((value) => ({
  value,
  label: KINDS[value].label,
}));

/**
 * Screen 147 — تاریخچه فعالیت ادمین‌ها.
 *
 * Owner only. The trail is written in the same transaction as the change it
 * describes, so a successful write with no row here is impossible — which is
 * what makes it worth reading, and why nothing on this screen can edit or
 * delete one.
 *
 * The screen used to be a search box over the first hundred rows: no way to
 * narrow to a kind of change, no date range, and no second page — so on an
 * installation with any history, everything older than the hundredth entry was
 * written, stored, and unreachable.
 */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRole('owner');

  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const action = first(params.action) ?? '';
  const fromDay = first(params.from) ?? '';
  const toDay = first(params.to) ?? '';
  const page = Math.max(1, Number(first(params.page) ?? 1) || 1);

  const { items: rows, total } = await getAuditLog({
    q: query,
    ...(action ? { action } : null),
    // The picker holds a day; the API takes an instant, and the two edges of a
    // day are not the same moment. See `instantFor`.
    ...(instantFor(fromDay, 'start') ? { from: instantFor(fromDay, 'start')! } : null),
    ...(instantFor(toDay, 'end') ? { to: instantFor(toDay, 'end')! } : null),
    page,
    pageSize: PAGE_SIZE,
  });

  return (
    <AdminPage
      title="تاریخچه فعالیت ادمین‌ها"
      description="هر تغییری که اپراتورها در پنل انجام داده‌اند، همان‌جا که انجام شده ثبت می‌شود. این صفحه فقط خواندنی است."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'سیستم و دسترسی' },
        { label: 'تاریخچه فعالیت' },
      ]}
    >
      <Suspense fallback={null}>
        <FilterBar
          searchPlaceholder="جستجوی کاربر، فعالیت یا هدف..."
          filters={[{ param: 'action', label: 'نوع فعالیت', options: KIND_OPTIONS }]}
        />
      </Suspense>

      <Suspense fallback={null}>
        <AuditDateRange from={fromDay} to={toDay} />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyIcon="history"
        emptyTitle="رکوردی یافت نشد"
        emptyDescription="با این فیلترها فعالیتی ثبت نشده است."
        pagination={{ page, pageSize: PAGE_SIZE, total, params, basePath: '/settings/audit' }}
      />

      <p className="text-caption text-on-surface-variant">
        مجموع {toPersianDigits(total)} فعالیت ثبت‌شده.
      </p>
    </AdminPage>
  );
}
