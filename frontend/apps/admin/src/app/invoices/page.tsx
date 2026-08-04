import type { Metadata } from 'next';
import Link from 'next/link';
import { Suspense } from 'react';
import { Code, Icon, buttonClasses, formatDate, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterBar } from '@/components/FilterBar';
import { KpiRow } from '@/components/KpiRow';
import { getInvoices } from '@/lib/api/invoices';
import type { InvoiceSummaryDto } from '@/lib/api/types';

export const metadata: Metadata = { title: 'مدیریت فاکتورها' };

type SearchParams = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

const PAGE_SIZE = 20;

const columns: Column<InvoiceSummaryDto>[] = [
  {
    key: 'invoiceNumber',
    header: 'شماره فاکتور',
    // Sixteen digits read as a reference, not as a quantity: Latin, monospaced
    // and isolated from the RTL flow so the browser cannot reorder them.
    cell: (invoice) => (
      <Code dir="ltr" className="font-semibold text-primary">
        {invoice.invoiceNumber}
      </Code>
    ),
  },
  {
    key: 'orderNumber',
    header: 'شماره سفارش',
    cell: (invoice) => <Code dir="ltr">{invoice.orderNumber}</Code>,
  },
  { key: 'customer', header: 'مشتری', cell: (invoice) => invoice.customer },
  {
    key: 'issuedAt',
    header: 'تاریخ صدور',
    cell: (invoice) => <span className="tabular">{formatDate(invoice.issuedAt)}</span>,
  },
  {
    key: 'items',
    header: 'اقلام',
    cell: (invoice) => <span className="tabular">{toPersianDigits(invoice.itemCount)} قلم</span>,
  },
  {
    key: 'total',
    header: 'مبلغ کل',
    cell: (invoice) => <span className="tabular">{formatPrice(invoice.total)}</span>,
  },
];

/** Invoice management — the issued invoices, searchable. */
export default async function AdminInvoicesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const query = (first(params.q) ?? '').trim();
  const page = Math.max(1, Number(first(params.page) ?? 1) || 1);

  const { items: rows, total } = await getInvoices({ q: query, page, pageSize: PAGE_SIZE });

  const billed = rows.reduce((sum, invoice) => sum + invoice.total, 0);

  return (
    <AdminPage
      title="مدیریت فاکتورها"
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'فاکتورها' }]}
      actions={
        // Owner-only on the API side. Shown to everyone who can reach this
        // screen for the reason the sidebar shows the wallet approvals link to
        // everyone: a control that vanishes without explanation is harder to
        // reason about than one that says no.
        <Link
          href="/invoices/settings"
          className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
        >
          <Icon name="settings" size={18} />
          تنظیمات فاکتور
        </Link>
      }
    >
      <KpiRow
        items={[
          { label: 'فاکتورهای صادرشده', value: toPersianDigits(total), icon: 'receipt_long' },
          { label: 'نمایش‌داده‌شده در این صفحه', value: toPersianDigits(rows.length), icon: 'list_alt' },
          { label: 'ارزش فاکتورهای این صفحه', value: formatPrice(billed), icon: 'payments' },
        ]}
      />

      <Suspense fallback={null}>
        <FilterBar searchPlaceholder="جستجوی شماره فاکتور، شماره سفارش یا نام مشتری..." />
      </Suspense>

      <p className="text-caption text-on-surface-variant">
        فاکتور فقط برای سفارش‌های تحویل‌شده صادر می‌شود؛ شماره فاکتور ۱۶ رقمی و یکتاست و پس از صدور
        تغییر نمی‌کند.
      </p>

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(invoice) => invoice.orderId}
        emptyTitle={query ? 'فاکتوری یافت نشد' : 'هنوز فاکتوری صادر نشده است'}
        emptyDescription={
          query
            ? 'با این عبارت فاکتوری پیدا نشد. شماره فاکتور، شماره سفارش یا نام مشتری را جستجو کنید.'
            : 'فاکتور پس از تحویل سفارش به‌صورت خودکار صادر می‌شود.'
        }
        emptyIcon="receipt_long"
        pagination={{
          page,
          pageSize: PAGE_SIZE,
          total,
          params,
          basePath: '/invoices',
        }}
        actions={(invoice) => (
          <Link
            href={`/invoices/${invoice.orderId}`}
            className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
          >
            مشاهده فاکتور
            <Icon name="chevron_left" size={16} />
          </Link>
        )}
      />
    </AdminPage>
  );
}
