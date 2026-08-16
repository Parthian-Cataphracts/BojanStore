import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import {
  Badge,
  Code,
  Icon,
  buttonClasses,
  formatDate,
  formatPrice,
  toPersianDigits,
} from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { getReturns } from '@/lib/api/returns';
import { requireRole } from '@/lib/auth/server';
import { returnStatusMeta } from '@/lib/status';
import type { AdminReturnDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت مرجوعی‌ها' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const PAGE_SIZE = 20;

const columns: Column<AdminReturnDto>[] = [
  {
    key: 'code',
    header: 'کد مرجوعی',
    cell: (row) => <Code className="text-primary font-semibold">{row.code}</Code>,
  },
  {
    key: 'order',
    header: 'سفارش',
    cell: (row) => <Code>{row.orderNumber}</Code>,
  },
  { key: 'customer', header: 'مشتری', cell: (row) => row.customerName },
  {
    key: 'items',
    header: 'اقلام',
    cell: (row) => (
      <span className="tabular">
        {toPersianDigits(row.items.reduce((sum, item) => sum + item.quantity, 0))}
      </span>
    ),
  },
  {
    /*
     * The figure, in the queue rather than only on the detail. An operator
     * triaging a morning's requests is deciding which to open, and what a
     * request is worth is most of that decision — sending them into each one to
     * find out is the difference between a queue and a list of links.
     */
    key: 'refund',
    header: 'مبلغ بازگشتی',
    cell: (row) =>
      row.status === 'refunded' ? (
        <span className="tabular text-primary">{formatPrice(row.refundAmount)}</span>
      ) : (
        <span className="tabular text-on-surface-variant">{formatPrice(row.refundEstimate)}</span>
      ),
  },
  {
    /*
     * Flagged in the list too, because it changes whether the request can be
     * finished at all: an order that was never paid for — a delivered
     * cash-on-delivery one nobody settled — has nothing to refund, and finding
     * that out after opening it is finding out too late.
     */
    key: 'payable',
    header: 'قابل بازپرداخت',
    cell: (row) =>
      row.payable ? (
        <Icon name="check_circle" size={20} className="text-primary" />
      ) : (
        <span className="gap-xs text-caption text-error flex items-center">
          <Icon name="money_off" size={18} />
          پرداخت نشده
        </span>
      ),
  },
  {
    key: 'date',
    header: 'تاریخ ثبت',
    cell: (row) => <span className="tabular">{formatDate(row.createdAt)}</span>,
  },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (row) => {
      const meta = returnStatusMeta[row.status];
      return <Badge tone={meta.tone}>{meta.label}</Badge>;
    },
  },
];

/*
  No «همه» of its own: `FilterBar` draws that chip for every filter group it is
  given, and highlights it while the param is unset. This list carried a second
  one — an option with an empty value — so the returns queue showed two «همه»
  side by side, of which only the shared one lit up. It is the only list screen
  that did; the other fourteen pass their statuses and let the bar add the chip.
*/
const statusFilters = Object.entries(returnStatusMeta).map(([value, meta]) => ({
  value,
  label: meta.label,
}));

/**
 * Screen 96 — the returns queue.
 *
 * The backend and its three endpoints have existed since the returns work, and
 * `return-decision` was declared in `resources.ts`, but nothing in the panel
 * ever opened them: an operator had no way to see a request, let alone decide
 * one, so every return a customer filed sat at "ثبت‌شده" for good and the
 * timeline on the customer's own screen drew a first step that never advanced.
 */
export default async function AdminReturnsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  // The same three roles the API admits, and the same ones as orders — a return
  // is the money half of an order.
  await requireRole('owner', 'sales', 'support');

  const params = await searchParams;
  const status = first(params.status) ?? '';
  const query = (first(params.q) ?? '').trim();
  const page = Math.max(1, Number(first(params.page) ?? 1) || 1);

  const { items: rows, total } = await getReturns({
    ...(status ? { status } : null),
    ...(query ? { q: query } : null),
    page,
    pageSize: PAGE_SIZE,
  });

  const waiting = rows.filter((row) => row.status !== 'refunded' && row.status !== 'rejected');
  const unpayable = waiting.filter((row) => !row.payable);

  return (
    <AdminPage
      title="مرجوعی‌ها"
      description="درخواست‌های بازگشت کالا، از ثبت تا بازپرداخت."
      breadcrumbs={[{ label: 'فروش' }, { label: 'مرجوعی‌ها' }]}
    >
      <Suspense fallback={null}>
        <FilterBar
          searchPlaceholder="کد مرجوعی، شماره سفارش یا نام مشتری"
          filters={[{ param: 'status', label: 'وضعیت', options: statusFilters }]}
        />
      </Suspense>

      {unpayable.length > 0 && (
        <p className="gap-sm bg-error-container p-md text-body-md text-on-error-container flex items-start rounded-lg">
          <Icon name="money_off" size={20} className="mt-px shrink-0" />
          <span>
            {toPersianDigits(unpayable.length)} درخواست باز روی سفارشی است که پولش هرگز وصول نشده.
            بازپرداخت آن‌ها رد می‌شود — معمولاً سفارش «پرداخت در محل» تسویه‌نشده‌اند.
          </span>
        </p>
      )}

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.id}
        emptyIcon="assignment_return"
        emptyTitle="درخواست مرجوعی‌ای نیست"
        emptyDescription="با فیلترهای فعلی هیچ درخواست بازگشت کالایی ثبت نشده است."
        actions={(row) => (
          <Link
            href={`/returns/${row.id}`}
            className={buttonClasses({ variant: 'outline', size: 'sm' })}
          >
            بررسی
          </Link>
        )}
      />

      <p className="text-caption text-on-surface-variant">
        مجموع {toPersianDigits(total)} درخواست — صفحه {toPersianDigits(page)}
      </p>
    </AdminPage>
  );
}
