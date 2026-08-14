import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { Badge, Card, Code, Icon, buttonClasses, formatDate, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { CustomerAccessPanel } from '@/components/CustomerAccessPanel';
import { CustomerEditPanel } from '@/components/CustomerEditPanel';
import { DataTable } from '@/components/DataTable';
import { getCustomer, getCustomers } from '@/lib/api/customers';
import { getOrders } from '@/lib/api/orders';
import { requireRole } from '@/lib/auth/server';
import { orderStatusMeta } from '@/lib/status';
import type { AdminOrderStatus } from '@/lib/types';

export const metadata: Metadata = { title: 'جزئیات مشتری' };

/** Screen 117 — Customer detail. */
export default async function CustomerDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const session = await requireRole('owner', 'sales', 'support');
  const { id } = await params;
  const customer = await getCustomer(id);
  if (!customer) notFound();

  // No order-by-customer filter on the backend yet; the customer's name
  // narrows the fetched page, same as the fixture path did.
  const { items: orders } = await getOrders({ q: customer.name, pageSize: 50 });

  // The groups already in use, so the edit form offers a shortlist rather than
  // asking an operator to retype a segment name exactly.
  const { items: everyone } = await getCustomers({ pageSize: 200 });
  const groups = [...new Set(everyone.map((entry) => entry.group).filter(Boolean))];

  return (
    <AdminPage
      title="جزئیات مشتری"
      breadcrumbs={[{ label: 'مشتریان', href: '/customers' }, { label: customer.name }]}
    >
      <Card className="flex flex-wrap items-center gap-md p-lg">
        <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-soft-mint font-headline text-card-title text-primary">
          {customer.name.charAt(0)}
        </span>

        <div className="flex min-w-0 flex-1 flex-col gap-xs">
          <h3 className="text-body-lg font-semibold text-primary">{customer.name}</h3>
          <span className="tabular text-caption text-on-surface-variant">
            {toPersianDigits(customer.phone)}
          </span>
          {customer.email && <Code className="text-caption text-outline">{customer.email}</Code>}
        </div>

        <Badge tone={customer.group === 'سازمانی' ? 'teal' : 'mint'}>{customer.group}</Badge>
      </Card>

      <section className="grid gap-md sm:grid-cols-2 xl:grid-cols-4">
        {[
          { label: 'تعداد سفارش', value: toPersianDigits(customer.orderCount), icon: 'receipt_long' },
          { label: 'مجموع خرید', value: formatPrice(customer.totalSpent), icon: 'payments' },
          {
            label: 'میانگین سبد',
            value: formatPrice(
              customer.orderCount > 0 ? Math.round(customer.totalSpent / customer.orderCount) : 0,
            ),
            icon: 'shopping_basket',
          },
          {
            label: 'عضویت از',
            value: formatDate(customer.joinedAt, 'long'),
            icon: 'calendar_today',
          },
        ].map((kpi) => (
          <Card key={kpi.label} className="flex flex-col gap-sm p-lg">
            <span className="flex h-10 w-10 items-center justify-center rounded-full bg-primary-fixed-dim/20 text-primary-container">
              <Icon name={kpi.icon} size={22} />
            </span>
            <span className="text-caption text-on-surface-variant">{kpi.label}</span>
            <span className="tabular text-body-lg font-semibold text-primary">{kpi.value}</span>
          </Card>
        ))}
      </section>

      <section className="flex flex-col gap-md">
        <h3 className="font-headline text-card-title text-primary">سفارش‌های این مشتری</h3>

        <DataTable
          rows={orders}
          rowKey={(row) => row.id}
          emptyTitle="سفارشی ثبت نشده"
          emptyDescription="این مشتری هنوز سفارشی ثبت نکرده است."
          columns={[
            { key: 'number', header: 'شماره سفارش', cell: (row) => <Code>#{row.number}</Code> },
            {
              key: 'date',
              header: 'تاریخ',
              cell: (row) => <span className="tabular">{formatDate(row.placedAt, 'long')}</span>,
            },
            { key: 'total', header: 'مبلغ', cell: (row) => formatPrice(row.total) },
            {
              key: 'status',
              header: 'وضعیت',
              cell: (row) => {
                const meta = orderStatusMeta[row.status as AdminOrderStatus];
                return <Badge tone={meta.tone}>{meta.label}</Badge>;
              },
            },
          ]}
          actions={(row) => (
            <Link
              href={`/orders/${row.id}`}
              className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
            >
              جزئیات
            </Link>
          )}
        />
      </section>

      {/*
        Sending this customer a message used to be a card on this page. It lives
        on «ارسال اعلان» now, with every other outbound message the shop sends —
        broadcast, SMS, browser push — because that is one job and it was split
        across two screens, one of which nobody thinks to look at when the task
        is "tell somebody something".
      */}
      <Card className="flex flex-wrap items-center justify-between gap-md p-md">
        <span className="flex items-start gap-sm text-caption leading-relaxed text-on-surface-variant">
          <Icon name="send" size={20} className="mt-px shrink-0 text-primary" />
          برای فرستادن پیام به این مشتری، از صفحه‌ی «ارسال اعلان» استفاده کنید.
        </span>
        <Link
          href={`/campaigns/notifications?customerId=${customer.id}`}
          className={buttonClasses({ variant: 'outline', size: 'sm', className: 'gap-xs' })}
        >
          <Icon name="send" size={18} />
          ارسال پیام به این مشتری
        </Link>
      </Card>

      {/*
        Owner only, and hidden rather than disabled for the others: the API
        refuses both writes for anyone below owner, so drawing the controls for
        a support operator would be offering a button that answers 403.
      */}
      {/*
        Editing the record is ordinary work for whoever handles this customer,
        so it is not owner-gated the way closing them out of their account is.
        The API applies the same split.
      */}
      <CustomerEditPanel customer={customer} groups={groups} />

      {session.role === 'owner' && (
        <CustomerAccessPanel customerId={customer.id} blocked={customer.status === 'blocked'} />
      )}
    </AdminPage>
  );
}
