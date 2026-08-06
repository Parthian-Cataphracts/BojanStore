import type { Metadata } from 'next';
import Link from 'next/link';
import { buttonClasses } from '@bojan/ui';
import { CartLineList } from '@/components/checkout/CartLineList';
import { ChosenAddressCard } from '@/components/checkout/ChosenAddressCard';
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

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader
        title="خلاصه سفارش"
        backHref={routes.checkoutReview}
        subtitle="لطفاً اطلاعات سفارش خود را بررسی کنید."
      />

      <ChosenAddressCard addresses={addresses} />

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
