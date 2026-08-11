'use client';

import { toPersianDigits } from '@bojan/ui';
import { CartTotals } from './CartTotals';
import { useCheckout } from '@/lib/checkout/store';
import type { ShippingMethod } from '@/lib/mock/checkout';

/**
 * Screen 78's recap — the choices the shopper actually made.
 *
 * The page is a server component, so it could only ever reach for
 * `shippingMethods[0]`: the last screen before payment showed the *first*
 * shipping method and its price whatever the shopper had picked two steps
 * earlier, and the delivery window they chose appeared nowhere at all.
 */
export function ConfirmSummary({
  phone,
  shippingMethods,
  freeShippingThreshold = 0,
}: {
  phone: string;
  shippingMethods: ShippingMethod[];
  /** The shop's free-delivery threshold, so this recap charges what the order will. */
  freeShippingThreshold?: number;
}) {
  const { selection, hydrated } = useCheckout();

  const chosen = shippingMethods.find((method) => method.id === selection.shippingMethodId);

  // Before hydration nothing is known yet; an em dash is honest where the
  // first method's name would be a guess.
  const shippingLabel = hydrated ? (chosen?.label ?? '—') : '—';
  const shippingPrice = chosen?.price ?? 0;

  return (
    <CartTotals
      shippingPrice={shippingPrice}
      freeShippingThreshold={freeShippingThreshold}
      showItemCount
      leadingRows={[
        { label: 'شماره موبایل', value: toPersianDigits(phone) },
        { label: 'روش ارسال', value: shippingLabel },
        ...(hydrated && selection.deliveryWindow
          ? [{ label: 'زمان تحویل', value: selection.deliveryWindow }]
          : []),
      ]}
    />
  );
}
