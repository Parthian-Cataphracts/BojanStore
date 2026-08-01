import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import {
  Badge,
  Card,
  Code,
  Icon,
  buttonClasses,
  formatDate,
  formatPrice,
  toPersianDigits,
} from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { OrderTimeline } from '@/components/account/OrderTimeline';
import { getOrder } from '@/lib/api/account';
import { orderStatusMeta } from '@/lib/mock/orders';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'جزئیات سفارش',
  robots: { index: false },
};

/** Screen 13 — Order detail. */
export default async function OrderDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const order = await getOrder(id);
  if (!order) notFound();

  const status = orderStatusMeta[order.status];

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="جزئیات سفارش" backHref={routes.orders} />

      {/* Summary */}
      <Card className="flex flex-wrap items-center justify-between gap-md p-lg">
        <div className="flex flex-col gap-xs">
          <span className="text-caption text-on-surface-variant">شماره سفارش</span>
          <Code className="text-body-lg font-semibold text-primary">#{order.number}</Code>
        </div>
        <div className="flex flex-col items-end gap-xs">
          <span className="text-caption text-on-surface-variant">وضعیت</span>
          <Badge tone={status.tone}>{status.label}</Badge>
        </div>
      </Card>

      <OrderTimeline steps={order.timeline} />

      {/* Items */}
      <section className="flex flex-col gap-md">
        <h2 className="font-headline text-display-md text-primary">محصولات سفارش</h2>

        <Card className="divide-y divide-paper-border">
          {order.items.map((item) => (
            <div key={item.productId} className="flex items-center gap-md p-md">
              <Link
                href={routes.product(item.slug)}
                className="relative h-16 w-16 shrink-0 overflow-hidden rounded border border-outline-variant"
              >
                <Image src={item.image} alt={item.title} fill sizes="64px" className="object-cover" />
              </Link>

              <div className="flex min-w-0 flex-1 flex-col gap-xs">
                <Link
                  href={routes.product(item.slug)}
                  className="line-clamp-2 text-body-md text-on-surface transition-colors hover:text-primary"
                >
                  {item.title}
                </Link>
                <span className="tabular text-caption text-on-surface-variant">
                  {toPersianDigits(item.quantity)} عدد
                </span>
              </div>

              <span className="tabular shrink-0 text-label-md font-label-md text-primary">
                {formatPrice(item.unitPrice * item.quantity)}
              </span>
            </div>
          ))}
        </Card>
      </section>

      {/* Delivery / shipping / payment */}
      <section className="grid gap-md md:grid-cols-3">
        {[
          { icon: 'location_on', title: 'آدرس تحویل', body: order.shippingAddress },
          { icon: 'local_shipping', title: 'روش ارسال', body: order.shippingMethod },
          { icon: 'credit_card', title: 'روش پرداخت', body: order.paymentMethod },
        ].map((info) => (
          <Card key={info.title} className="flex flex-col gap-sm p-lg">
            <span className="flex items-center gap-sm text-label-md font-label-md text-primary">
              <Icon name={info.icon} size={20} />
              {info.title}
            </span>
            <p className="text-body-md leading-relaxed text-on-surface-variant">{info.body}</p>
          </Card>
        ))}
      </section>

      {order.trackingCode && (
        <Card className="flex flex-wrap items-center justify-between gap-md p-lg">
          <span className="flex items-center gap-sm text-label-md font-label-md text-primary">
            <Icon name="local_shipping" size={20} />
            کد رهگیری مرسوله
          </span>
          <span className="tabular text-body-md text-on-surface">{order.trackingCode}</span>
        </Card>
      )}

      {/* Totals */}
      <Card className="flex flex-col gap-sm p-lg">
        <dl className="flex flex-col gap-sm text-body-md">
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">جمع مبلغ کالاها</dt>
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
            <dt className="text-body-lg font-label-md text-primary">مبلغ کل پرداخت شده</dt>
            <dd className="tabular text-body-lg font-label-md text-primary">
              {formatPrice(order.total)}
            </dd>
          </div>
        </dl>

        <p className="tabular mt-sm text-caption text-outline">
          ثبت شده در {formatDate(order.placedAt, 'long')}
        </p>
      </Card>

      {/* Order actions. A return is only offered once the goods have arrived. */}
      <div className="flex flex-col gap-md sm:flex-row">
        <Link
          href={routes.invoice(order.id)}
          className={buttonClasses({
            variant: 'outline',
            size: 'lg',
            fullWidth: true,
            className: 'gap-sm',
          })}
        >
          <Icon name="receipt_long" size={20} />
          مشاهده فاکتور
        </Link>

        {order.status === 'delivered' && (
          <Link
            href={routes.returnRequest(order.id)}
            className={buttonClasses({
              variant: 'outline',
              size: 'lg',
              fullWidth: true,
              className: 'gap-sm',
            })}
          >
            <Icon name="assignment_return" size={20} />
            درخواست مرجوعی
          </Link>
        )}
      </div>

      <Link
        href={routes.contact}
        className="paper-card flex items-center justify-center gap-sm rounded-lg p-lg text-label-md font-label-md text-primary transition-shadow hover:shadow-soft"
      >
        <Icon name="support_agent" size={20} />
        نیاز به پیگیری دارید؟ تماس با پشتیبانی
      </Link>
    </Container>
  );
}
