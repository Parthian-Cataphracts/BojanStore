import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { CartLineList } from '@/components/checkout/CartLineList';
import {
  ChosenShippingCard,
  ChosenShippingTotals,
} from '@/components/checkout/ChosenShippingCard';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { getAddresses } from '@/lib/api/account';
import { getShippingMethods } from '@/lib/api/cart';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'خلاصه سفارش',
  robots: { index: false },
};

/** Screen 79 — Order summary. */
export default async function CheckoutSummaryPage() {
  const [shippingMethods, addresses] = await Promise.all([getShippingMethods(), getAddresses()]);
  const address = addresses.find((item) => item.isDefault) ?? addresses[0];

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
              <Icon name="place" size={20} />
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

      <ChosenShippingCard shippingMethods={shippingMethods} />

      <CartLineList />

      <ChosenShippingTotals shippingMethods={shippingMethods} />

      <Link
        href={routes.checkoutConfirm}
        className={buttonClasses({ size: 'lg', fullWidth: true, className: 'md:w-auto md:self-start md:px-xl' })}
      >
        تایید و پرداخت
      </Link>
    </Container>
  );
}
