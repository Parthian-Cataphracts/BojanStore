import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Badge, Card, Code, Icon, formatDate, formatPrice, toPersianDigits } from '@bojan/ui';
import { AdminPage } from '@/components/AdminPage';
import { OrderStatusControl } from '@/components/OrderStatusControl';
import { getOrder } from '@/lib/api/orders';
import { orderStatusMeta } from '@/lib/status';
import type { AdminOrderStatus } from '@/lib/types';

export const metadata: Metadata = { title: 'جزئیات سفارش' };

/** Screens 94 and 95 — Order detail with the status control. */
export default async function AdminOrderDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const order = await getOrder(id);
  if (!order) notFound();

  const status = order.status as AdminOrderStatus;
  const meta = orderStatusMeta[status];

  return (
    <AdminPage
      title={`سفارش ${order.number}`}
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'سفارش‌ها', href: '/orders' },
        { label: order.number },
      ]}
      actions={<Badge tone={meta.tone}>{meta.label}</Badge>}
    >
      <div className="grid gap-lg lg:grid-cols-[1fr_320px] lg:items-start">
        <div className="flex flex-col gap-lg">
          {/* Items */}
          <Card surface="plain" className="overflow-hidden">
            <h3 className="border-b border-outline-variant/40 px-lg py-md font-headline text-card-title text-primary">
              اقلام سفارش
            </h3>

            <div className="overflow-x-auto">
              <table className="w-full min-w-[520px] text-start">
                <thead className="bg-surface-container-low text-on-surface-variant">
                  <tr>
                    <th scope="col" className="px-lg py-sm text-start">کالا</th>
                    <th scope="col" className="px-lg py-sm text-start">کد</th>
                    <th scope="col" className="px-lg py-sm text-start">تعداد</th>
                    <th scope="col" className="px-lg py-sm text-start">جمع</th>
                  </tr>
                </thead>
                <tbody>
                  {order.items.map((item) => (
                    <tr key={item.sku} className="border-t border-outline-variant/30">
                      <td className="px-lg py-md text-on-surface">{item.title}</td>
                      <td className="px-lg py-md">
                        <Code className="text-caption text-on-surface-variant">{item.sku}</Code>
                      </td>
                      <td className="tabular px-lg py-md text-on-surface-variant">
                        {toPersianDigits(item.quantity)}
                      </td>
                      <td className="tabular px-lg py-md font-medium text-primary">
                        {formatPrice(item.unitPrice * item.quantity)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          {/* Customer / delivery */}
          <div className="grid gap-lg md:grid-cols-2">
            <Card className="flex flex-col gap-sm p-lg">
              <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
                <Icon name="person" size={20} />
                مشتری
              </h3>
              <p className="text-body-md text-on-surface">{order.customer}</p>
              <p className="tabular text-caption text-on-surface-variant">
                {toPersianDigits(order.customerPhone)}
              </p>
            </Card>

            <Card className="flex flex-col gap-sm p-lg">
              <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
                <Icon name="local_shipping" size={20} />
                ارسال
              </h3>
              <p className="text-body-md text-on-surface">{order.shippingMethod}</p>
              <p className="text-caption leading-relaxed text-on-surface-variant">{order.address}</p>

              {/* Only when the shopper asked for one — most orders come
                  through the single-page checkout, which does not offer it. */}
              {order.deliveryWindow && (
                <p className="flex items-center gap-xs text-caption text-on-surface-variant">
                  <Icon name="schedule" size={16} className="shrink-0 text-primary" />
                  زمان درخواستی: {order.deliveryWindow}
                </p>
              )}
            </Card>
          </div>
        </div>

        {/* Sidebar */}
        <div className="flex flex-col gap-lg lg:sticky lg:top-24">
          <OrderStatusControl orderId={order.id} current={status} />

          <Card className="flex flex-col gap-sm p-lg">
            <h3 className="font-headline text-card-title text-primary">خلاصه</h3>
            <dl className="flex flex-col gap-sm">
              {[
                { label: 'تاریخ ثبت', value: formatDate(order.placedAt, 'long') },
                { label: 'روش پرداخت', value: order.paymentMethod },
                { label: 'تعداد اقلام', value: toPersianDigits(order.itemCount) },
              ].map((row) => (
                <div
                  key={row.label}
                  className="flex items-center justify-between gap-md border-b border-paper-border pb-sm last:border-0 last:pb-0"
                >
                  <dt className="text-caption text-on-surface-variant">{row.label}</dt>
                  <dd className="tabular text-caption text-on-surface">{row.value}</dd>
                </div>
              ))}
              <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
                <dt className="text-body-md font-semibold text-primary">مبلغ کل</dt>
                <dd className="tabular text-body-md font-semibold text-primary">
                  {formatPrice(order.total)}
                </dd>
              </div>
            </dl>
          </Card>
        </div>
      </div>
    </AdminPage>
  );
}
