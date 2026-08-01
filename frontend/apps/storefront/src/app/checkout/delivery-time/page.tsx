import type { Metadata } from 'next';
import { formatPrice } from '@bojan/ui';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { DeliveryTimePicker } from '@/components/checkout/DeliveryTimePicker';
import { shippingMethods, upcomingDeliveryDays } from '@/lib/mock/checkout';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'انتخاب زمان تحویل',
  robots: { index: false },
};

/** Screen 74 — Choose a delivery day and time window. */
export default function CheckoutDeliveryTimePage() {
  const days = upcomingDeliveryDays(5);

  return (
    <CheckoutShell
      step="shipping"
      title="انتخاب زمان تحویل"
      description="لطفاً روز و بازه زمانی مناسب برای دریافت سفارش خود را انتخاب کنید."
      showSummary
      extraRows={[{ label: 'هزینه ارسال', value: formatPrice(shippingMethods[0]!.price) }]}
      nextHref={routes.checkoutPayment}
      nextLabel="ادامه به پرداخت"
      backHref={routes.checkoutShipping}
    >
      <DeliveryTimePicker days={days} />
    </CheckoutShell>
  );
}
