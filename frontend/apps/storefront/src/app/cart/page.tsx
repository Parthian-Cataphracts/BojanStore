import type { Metadata } from 'next';
import { Container } from '@/components/layout/Container';
import { CartView } from '@/components/cart/CartView';

export const metadata: Metadata = {
  title: 'سبد خرید',
  robots: { index: false },
};

/**
 * Screen 07 — Cart.
 *
 * The basket comes from the cart store the layout provides, not from this page:
 * it has to stay in step with the header count and with whatever the shopper
 * added on the product screens.
 */
export default function CartPage() {
  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <h1 className="font-headline text-display-md text-primary md:text-headline-lg">سبد خرید</h1>
      <CartView />
    </Container>
  );
}
