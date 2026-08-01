import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Code, Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getCurrentUser } from '@/lib/api/account';
import { mockCart } from '@/lib/mock/catalog';
import { shippingMethods } from '@/lib/mock/checkout';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'تایید نهایی',
  robots: { index: false },
};

/** Screen 78 — Confirm details before handing off to the payment gateway. */
export default async function CheckoutConfirmPage() {
  const user = await getCurrentUser();
  const shipping = shippingMethods[0]!;
  const total = mockCart.subtotal - mockCart.discount + shipping.price;

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="تایید نهایی" backHref={routes.checkoutReview} />

      <Card className="flex items-start gap-md p-lg">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-soft-mint text-primary">
          <Icon name="security" size={24} />
        </span>
        <div className="flex flex-col gap-xs">
          <h2 className="text-body-lg font-semibold text-primary">اطلاعات پرداخت</h2>
          <p className="text-body-md leading-relaxed text-on-surface-variant">
            لطفاً پیش از انتقال به درگاه بانکی، اطلاعات را بررسی نمایید.
          </p>
        </div>
      </Card>

      <Card className="flex flex-col gap-sm p-lg">
        <dl className="flex flex-col gap-sm">
          {[
            { label: 'شماره سفارش (موقت)', value: <Code>#ORD-9482</Code> },
            { label: 'شماره موبایل', value: toPersianDigits(user.phone) },
            { label: 'تعداد کالاها', value: `${toPersianDigits(mockCart.lines.length)} کالا` },
            { label: 'روش ارسال', value: shipping.label },
            { label: 'هزینه ارسال', value: formatPrice(shipping.price) },
          ].map((row) => (
            <div
              key={row.label}
              className="flex items-center justify-between gap-md border-b border-paper-border pb-sm last:border-0 last:pb-0"
            >
              <dt className="text-caption text-on-surface-variant">{row.label}</dt>
              <dd className="tabular text-body-md text-on-surface">{row.value}</dd>
            </div>
          ))}

          <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
            <dt className="text-body-lg font-semibold text-primary">مبلغ قابل پرداخت</dt>
            <dd className="tabular text-body-lg font-semibold text-primary">
              {formatPrice(total)}
            </dd>
          </div>
        </dl>
      </Card>

      <Card className="flex items-start gap-sm p-md">
        <Icon name="lock" size={20} className="mt-px shrink-0 text-primary" />
        <p className="text-caption leading-relaxed text-on-surface-variant">
          با زدن دکمه زیر به درگاه بانکی منتقل می‌شوید. اطلاعات کارت شما فقط روی صفحه بانک وارد
          می‌شود و بوژان به آن دسترسی ندارد.
        </p>
      </Card>

      <div className="flex flex-col gap-md sm:flex-row">
        <Link
          href={routes.paymentSuccess}
          className={buttonClasses({ size: 'lg', fullWidth: true, className: 'gap-sm' })}
        >
          <Icon name="lock" size={20} />
          انتقال به درگاه پرداخت
        </Link>
        <Link
          href={routes.checkoutEdit}
          className={buttonClasses({ variant: 'outline', size: 'lg', fullWidth: true })}
        >
          تغییر اطلاعات سفارش
        </Link>
      </div>
    </Container>
  );
}
