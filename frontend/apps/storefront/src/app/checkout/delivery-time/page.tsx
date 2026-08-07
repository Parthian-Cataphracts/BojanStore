import type { Metadata } from 'next';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { DeliveryTimePicker } from '@/components/checkout/DeliveryTimePicker';
import { upcomingDeliveryDays } from '@/lib/mock/checkout';
import { getShippingMethods } from '@/lib/api/cart';
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
  title: 'انتخاب زمان تحویل',
  robots: { index: false },
};

/** Screen 74 — Choose a delivery day and time window. */
export default async function CheckoutDeliveryTimePage() {
  const shippingMethods = await getShippingMethods();
  const days = upcomingDeliveryDays(5);

  return (
    <CheckoutShell
      step="shipping"
      title="انتخاب زمان تحویل"
      description="لطفاً روز و بازه زمانی مناسب برای دریافت سفارش خود را انتخاب کنید."
      showSummary
      shippingMethods={shippingMethods}
      nextHref={routes.checkoutPayment}
      nextLabel="ادامه به پرداخت"
      backHref={routes.checkoutShipping}
    >
      <DeliveryTimePicker days={days} />
    </CheckoutShell>
  );
}
