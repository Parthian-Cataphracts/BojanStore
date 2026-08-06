import type { Metadata } from 'next';
import Link from 'next/link';
import { CartLineList } from '@/components/checkout/CartLineList';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { ReviewRecap } from '@/components/checkout/ReviewRecap';
import { getAddresses } from '@/lib/api/account';
import { getPaymentMethods, getShippingMethods } from '@/lib/api/cart';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'بررسی نهایی سفارش',
  robots: { index: false },
};

/** Screen 77 — Final review before payment. */
export default async function CheckoutReviewPage() {
  const [shippingMethods, paymentMethods, addresses] = await Promise.all([
    getShippingMethods(),
    getPaymentMethods(),
    getAddresses(),
  ]);

  return (
    <CheckoutShell
      step="payment"
      title="بررسی نهایی سفارش"
      showSummary
      shippingMethods={shippingMethods}
      nextHref={routes.checkoutConfirm}
      nextLabel="تایید و ادامه"
      backHref={routes.checkoutPayment}
    >
      {/* Items */}
      <section className="flex flex-col gap-md">
        <div className="flex items-center justify-between gap-md">
          <h2 className="font-headline text-card-title text-primary">محصولات</h2>
          <Link
            href={routes.cart}
            className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
          >
            ویرایش
          </Link>
        </div>

        <CartLineList />
      </section>

      {/* Delivery + payment recap */}
      <ReviewRecap
        addresses={addresses}
        shippingMethods={shippingMethods}
        paymentMethods={paymentMethods}
      />
    </CheckoutShell>
  );
}
