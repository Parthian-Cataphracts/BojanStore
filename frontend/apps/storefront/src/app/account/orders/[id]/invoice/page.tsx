import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Card, Code, Icon, formatDate, formatPrice, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { InvoiceActions } from '@/components/account/InvoiceActions';
import { getCurrentUser, getOrder } from '@/lib/api/account';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'فاکتور سفارش',
  robots: { index: false },
};

/** Screen 34 — Order invoice. */
export default async function InvoicePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const [order, user] = await Promise.all([getOrder(id), getCurrentUser()]);
  if (!order) notFound();

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="فاکتور سفارش" backHref={`${routes.orders}/${order.id}`} />

      {/* The printable region — actions and chrome stay out of it. */}
      <div id="invoice" className="flex flex-col gap-lg">
        <Card className="flex flex-col gap-md p-lg">
          <div className="flex flex-wrap items-center justify-between gap-sm border-b border-paper-border pb-md">
            <span className="text-caption text-on-surface-variant">شماره فاکتور</span>
            <Code className="text-label-md font-semibold text-primary">
              INV-{order.number}
            </Code>
          </div>

          <dl className="grid gap-sm sm:grid-cols-2">
            {[
              { label: 'شماره سفارش', value: `#${order.number}` },
              { label: 'مشتری', value: `${user.firstName} ${user.lastName}` },
              { label: 'تاریخ', value: formatDate(order.placedAt, 'long') },
              { label: 'روش پرداخت', value: order.paymentMethod },
            ].map((row) => (
              <div key={row.label} className="flex items-center justify-between gap-md">
                <dt className="text-caption text-on-surface-variant">{row.label}</dt>
                <dd className="tabular text-body-md text-on-surface">{row.value}</dd>
              </div>
            ))}
          </dl>
        </Card>

        <Card surface="plain" className="overflow-hidden">
          <h2 className="border-b border-outline-variant/40 px-lg py-md font-headline text-display-md text-primary">
            محصولات
          </h2>

          <div className="overflow-x-auto">
            <table className="w-full min-w-[420px] text-start text-body-md">
              <thead className="bg-surface-container-low text-caption text-on-surface-variant">
                <tr>
                  <th scope="col" className="px-lg py-sm text-start">
                    کالا
                  </th>
                  <th scope="col" className="px-lg py-sm text-start">
                    تعداد
                  </th>
                  <th scope="col" className="px-lg py-sm text-start">
                    مبلغ
                  </th>
                </tr>
              </thead>
              <tbody>
                {order.items.map((item) => (
                  <tr key={item.productId} className="border-t border-outline-variant/30">
                    <td className="px-lg py-md text-on-surface">{item.title}</td>
                    <td className="tabular px-lg py-md text-on-surface-variant">
                      ×{toPersianDigits(item.quantity)}
                    </td>
                    <td className="tabular px-lg py-md text-primary">
                      {formatPrice(item.unitPrice * item.quantity)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>

        <Card className="flex flex-col gap-sm p-lg">
          <dl className="flex flex-col gap-sm text-body-md">
            <div className="flex items-center justify-between">
              <dt className="text-on-surface-variant">جمع کالاها</dt>
              <dd className="tabular text-on-surface">{formatPrice(order.subtotal)}</dd>
            </div>
            {order.discount > 0 && (
              <div className="flex items-center justify-between">
                <dt className="text-on-surface-variant">تخفیف</dt>
                <dd className="tabular text-secondary">−{formatPrice(order.discount)}</dd>
              </div>
            )}
            <div className="flex items-center justify-between">
              <dt className="text-on-surface-variant">هزینه ارسال</dt>
              <dd className="tabular text-on-surface">{formatPrice(order.shipping)}</dd>
            </div>
            <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
              <dt className="text-body-lg font-label-md text-primary">مبلغ پرداخت‌شده</dt>
              <dd className="tabular text-body-lg font-label-md text-primary">
                {formatPrice(order.total)}
              </dd>
            </div>
          </dl>
        </Card>
      </div>

      <InvoiceActions orderNumber={order.number} />

      <p className="flex items-center justify-center gap-xs text-caption text-outline">
        <Icon name="info" size={16} />
        این فاکتور به‌صورت الکترونیکی صادر شده و نیازی به مهر و امضا ندارد.
      </p>
    </Container>
  );
}
