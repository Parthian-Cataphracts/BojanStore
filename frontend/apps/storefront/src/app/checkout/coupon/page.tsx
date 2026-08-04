import type { Metadata } from 'next';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { CouponForm } from '@/components/checkout/CouponForm';
import { getCoupons } from '@/lib/api/activity';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'اعمال کد تخفیف',
  robots: { index: false },
};

/** Screen 76 — Apply a discount code. */
export default async function CheckoutCouponPage() {
  const coupons = await getCoupons();

  return (
    <CheckoutShell
      step="payment"
      title="کد تخفیف"
      showSummary
      nextHref={routes.checkoutReview}
      nextLabel="ادامه"
      backHref={routes.checkoutPayment}
    >
      <CouponForm coupons={coupons} />
    </CheckoutShell>
  );
}
