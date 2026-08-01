import type { Metadata } from 'next';
import { Breadcrumb } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { CheckoutForm } from '@/components/checkout/CheckoutForm';
import { getAddresses } from '@/lib/api/account';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'پرداخت و ثبت سفارش',
  robots: { index: false },
};

/**
 * Screen 08 — Checkout.
 *
 * The middleware has already established there is a session; addresses come
 * from the data layer, and the basket from the cart store.
 */
export default async function CheckoutPage() {
  const addresses = await getAddresses();

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <Breadcrumb
        items={[
          { label: 'خانه', href: routes.home },
          { label: 'سبد خرید', href: routes.cart },
          { label: 'پرداخت و ثبت سفارش' },
        ]}
      />

      <h1 className="font-headline text-display-md text-primary md:text-headline-lg">
        پرداخت و ثبت سفارش
      </h1>

      <CheckoutForm addresses={addresses} />
    </Container>
  );
}
