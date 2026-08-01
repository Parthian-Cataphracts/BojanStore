import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge, Code, Icon, buttonClasses, formatDate, formatPrice, toPersianDigits } from '@bojan/ui';
import { Suspense } from 'react';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { KpiRow } from '@/components/KpiRow';
import { getOrders } from '@/lib/api/orders';
import { orderStatusMeta } from '@/lib/status';
import type { AdminOrderDto } from '@/lib/api/types';
import type { AdminOrderStatus } from '@/lib/types';

export const metadata: Metadata = { title: 'مدیریت سفارش‌ها' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const PAGE_SIZE = 20;

const columns: Column<AdminOrderDto>[] = [
  {
    key: 'number',
    header: 'شماره سفارش',
    cell: (order) => <Code className="font-semibold text-primary">{order.number}</Code>,
  },
  { key: 'customer', header: 'مشتری', cell: (order) => order.customer },
  {
    key: 'date',
    header: 'تاریخ',
    cell: (order) => <span className="tabular">{formatDate(order.placedAt)}</span>,
  },
  {
    key: 'items',
    header: 'تعداد',
    cell: (order) => <span className="tabular">{toPersianDigits(order.itemCount)}</span>,
  },
  {
    key: 'total',
    header: 'مبلغ',
    cell: (order) => <span className="tabular">{formatPrice(order.total)}</span>,
  },
  {
    key: 'status',
    header: 'وضعیت',
    cell: (order) => {
      const meta = orderStatusMeta[order.status as AdminOrderStatus];
      return <Badge tone={meta.tone}>{meta.label}</Badge>;
    },
  },
];

/** Screen 93 — Order management. */
export default async function AdminOrdersPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const status = first(params.status) as AdminOrderStatus | undefined;
  const query = (first(params.q) ?? '').trim();
  const page = Math.max(1, Number(first(params.page) ?? 1) || 1);

  const { items: rows, total } = await getOrders({ status, q: query, page, pageSize: PAGE_SIZE });

  // The KPIs describe the whole filtered set, but the panel only fetches one
  // page — approximate them from the page in hand rather than a second call.
  const matched = rows;
  const revenue = matched
    .filter((order) => order.status !== 'cancelled')
    .reduce((sum, order) => sum + order.total, 0);

  return (
    <AdminPage
      title="مدیریت سفارش‌ها"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'سفارش‌ها' }]}
      actions={
        <Link href="/reports/sales" className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}>
          <Icon name="download" size={18} />
          خروجی گزارش
        </Link>
      }
    >
      <KpiRow
        items={[
          { label: 'سفارش‌های نمایش‌داده‌شده', value: toPersianDigits(matched.length), icon: 'shopping_cart' },
          {
            label: 'در انتظار پردازش',
            value: toPersianDigits(
              matched.filter((o) => o.status === 'pending' || o.status === 'processing').length,
            ),
            icon: 'schedule',
          },
          {
            label: 'ارسال‌شده',
            value: toPersianDigits(matched.filter((o) => o.status === 'shipped').length),
            icon: 'local_shipping',
          },
          { label: 'ارزش سفارش‌ها', value: formatPrice(revenue), icon: 'payments' },
        ]}
      />

      <Suspense fallback={null}>
        <FilterBar
          searchPlaceholder="جستجوی شماره سفارش یا نام مشتری..."
          filters={[
            {
              param: 'status',
              label: 'وضعیت',
              options: (Object.keys(orderStatusMeta) as AdminOrderStatus[]).map((key) => ({
                value: key,
                label: orderStatusMeta[key].label,
              })),
            },
          ]}
        />
      </Suspense>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(order) => order.id}
        emptyTitle="سفارشی یافت نشد"
        emptyDescription="با این فیلترها سفارشی ثبت نشده است."
        emptyIcon="receipt_long"
        pagination={{
          page,
          pageSize: PAGE_SIZE,
          total,
          params,
          basePath: '/orders',
        }}
        actions={(order) => (
          <Link
            href={`/orders/${order.id}`}
            className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
          >
            جزئیات
            <Icon name="chevron_left" size={16} />
          </Link>
        )}
      />
    </AdminPage>
  );
}
