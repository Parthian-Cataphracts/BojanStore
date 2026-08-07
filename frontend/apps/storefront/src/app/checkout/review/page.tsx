import type { Metadata } from 'next';
import Link from 'next/link';
import { CartLineList } from '@/components/checkout/CartLineList';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { ReviewRecap } from '@/components/checkout/ReviewRecap';
import { getAddresses } from '@/lib/api/account';
import { getPaymentMethods, getShippingMethods } from '@/lib/api/cart';
import { routes } from '@/lib/routes';

/*
 * Rendered on request, not at build.
 *
 * This page reads the catalogue, and the catalogue lives behind the API — which
 * does not exist when the image is built. Prerendering it meant `next build`
 * fetching from a host that is not up yet, which is exactly how the Docker
 * build failed. The alternative, emitting it with whatever an unreachable API
 * returns, is worse: the first visitors after a deploy would be served an empty
 * shop until the first revalidation filled it in.
 *
 * Nothing is lost by it. The fetches underneath already declare their own
 * `revalidate` window, so the API is not called per request either way — the
 * caching just happens a layer down, where stock and prices can expire on their
 * own schedule instead of being frozen into the image.
 */
export const dynamic = 'force-dynamic';

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
