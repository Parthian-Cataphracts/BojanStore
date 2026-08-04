import type { Metadata } from 'next';
import Link from 'next/link';
import { Card, Icon, buttonClasses } from '@bojan/ui';
import { CartLineList } from '@/components/checkout/CartLineList';
import {
  ChosenShippingLine,
  ChosenShippingTotals,
} from '@/components/checkout/ChosenShipping';
import { SelectedAddressCard } from '@/components/checkout/SelectedAddressCard';
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
  const [addresses, shippingMethods] = await Promise.all([getAddresses(), getShippingMethods()]);

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="خلاصه سفارش"
        backHref={routes.checkoutReview}
        subtitle="لطفاً اطلاعات سفارش خود را بررسی کنید."
      />

      <SelectedAddressCard addresses={addresses} />

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

        <ChosenShippingLine shippingMethods={shippingMethods} />
      </Card>

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
