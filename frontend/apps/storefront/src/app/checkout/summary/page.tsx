import type { Metadata } from 'next';
import Image from 'next/image';
import Link from 'next/link';
import { Card, Icon, buttonClasses, formatPrice, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getAddresses } from '@/lib/api/account';
import { mockCart } from '@/lib/mock/catalog';
import { shippingMethods } from '@/lib/mock/checkout';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'خلاصه سفارش',
  robots: { index: false },
};

/** Screen 79 — Order summary. */
export default async function CheckoutSummaryPage() {
  const addresses = await getAddresses();
  const address = addresses.find((item) => item.isDefault) ?? addresses[0];
  const shipping = shippingMethods[2]!;
  const total = mockCart.subtotal - mockCart.discount + shipping.price;

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="خلاصه سفارش"
        backHref={routes.checkoutReview}
        subtitle="لطفاً اطلاعات سفارش خود را بررسی کنید."
      />

      {address && (
        <Card className="flex flex-col gap-sm p-lg">
          <div className="flex items-center justify-between gap-md">
            <h2 className="flex items-center gap-xs text-label-md font-semibold text-primary">
              <Icon name="location_on" size={20} />
              آدرس تحویل
            </h2>
            <Link
              href={routes.checkoutAddress}
              className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
            >
              ویرایش
            </Link>
          </div>

          <p className="text-body-md text-on-surface">{address.recipient}</p>
          <p className="text-body-md leading-relaxed text-on-surface-variant">
            {address.province}، {address.city}، {address.line}
          </p>
          <p className="tabular text-caption text-outline">
            کد پستی: {toPersianDigits(address.postalCode)} · شماره تماس:{' '}
            {toPersianDigits(address.phone)}
          </p>
        </Card>
      )}

      <Card className="flex flex-col gap-sm p-lg">
        <div className="flex items-center justify-between gap-md">
          <h2 className="flex items-center gap-xs text-label-md font-semibold text-primary">
            <Icon name="local_shipping" size={20} />
            نحوه ارسال
          </h2>
          <Link
            href={routes.checkoutShipping}
            className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
          >
            ویرایش
          </Link>
        </div>

        <span className="flex items-center gap-sm">
          <Icon name={shipping.icon} size={22} className="text-primary" />
          <span className="flex flex-col">
            <span className="text-body-md text-on-surface">{shipping.label}</span>
            <span className="text-caption text-on-surface-variant">{shipping.note}</span>
          </span>
        </span>
      </Card>

      <Card className="divide-y divide-paper-border">
        {mockCart.lines.map((line) => (
          <div key={line.id} className="flex items-center gap-md p-md">
            <span className="relative h-16 w-16 shrink-0 overflow-hidden rounded border border-outline-variant">
              <Image src={line.image} alt={line.title} fill sizes="64px" className="object-cover" />
            </span>
            <span className="flex min-w-0 flex-1 flex-col gap-xs">
              <span className="line-clamp-2 text-body-md text-on-surface">{line.title}</span>
              <span className="tabular text-caption text-on-surface-variant">
                {toPersianDigits(line.quantity)} عدد
              </span>
            </span>
            <span className="tabular shrink-0 text-label-md font-semibold text-primary">
              {formatPrice(line.unitPrice * line.quantity)}
            </span>
          </div>
        ))}
      </Card>

      <Card className="flex flex-col gap-sm p-lg">
        <dl className="flex flex-col gap-sm text-body-md">
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">جمع کالاها</dt>
            <dd className="tabular text-on-surface">{formatPrice(mockCart.subtotal)}</dd>
          </div>
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">تخفیف</dt>
            <dd className="tabular text-secondary">−{formatPrice(mockCart.discount)}</dd>
          </div>
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">هزینه ارسال</dt>
            <dd className="tabular text-on-surface">{formatPrice(shipping.price)}</dd>
          </div>
          <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
            <dt className="text-body-lg font-semibold text-primary">مبلغ قابل پرداخت</dt>
            <dd className="tabular text-body-lg font-semibold text-primary">{formatPrice(total)}</dd>
          </div>
        </dl>
      </Card>

      <Link
        href={routes.checkoutConfirm}
        className={buttonClasses({ size: 'lg', fullWidth: true, className: 'md:w-auto md:self-start md:px-xl' })}
      >
        تایید و پرداخت
      </Link>
    </Container>
  );
}
