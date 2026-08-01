import type { Metadata } from 'next';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { CouponForm } from '@/components/checkout/CouponForm';
import { mockCart } from '@/lib/mock/catalog';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'اعمال کد تخفیف',
  robots: { index: false },
};

/** Screen 76 — Apply a discount code. */
export default function CheckoutCouponPage() {
  return (
    <CheckoutShell
      step="payment"
      title="کد تخفیف"
      cart={mockCart}
      nextHref={routes.checkoutReview}
      nextLabel="ادامه"
      backHref={routes.checkoutPayment}
    >
      <CouponForm />
    </CheckoutShell>
  );
}
