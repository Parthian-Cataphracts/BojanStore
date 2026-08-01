import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { CartLineList } from '@/components/checkout/CartLineList';
import { CartTotals } from '@/components/checkout/CartTotals';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getAddresses } from '@/lib/api/account';
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

      <CartLineList />

      <CartTotals shippingPrice={shipping.price} />

      <Link
        href={routes.checkoutConfirm}
        className={buttonClasses({ size: 'lg', fullWidth: true, className: 'md:w-auto md:self-start md:px-xl' })}
      >
        تایید و پرداخت
      </Link>
    </Container>
  );
}
