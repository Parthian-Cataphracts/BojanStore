'use client';

import { toPersianDigits } from '@bojan/ui';
import type { CheckoutShippingMethod } from '@/lib/api/cart';
import { useCheckoutSelection } from '@/lib/checkout/store';
import { CartTotals } from './CartTotals';

/**
 * The totals on the confirm screen, priced against the tier the shopper chose.
 *
 * It used to take `shippingMethods[0]` — so a shopper who picked the courier
 * tier was shown the standard fee on the last screen before paying, and then
 * charged the courier one.
 */
export function ConfirmSummary({
  phone,
  shippingMethods,
}: {
  phone: string;
  shippingMethods: CheckoutShippingMethod[];
}) {
  const { selection } = useCheckoutSelection();

  const shipping =
    shippingMethods.find((method) => method.id === selection.shippingMethodId) ??
    shippingMethods[0];

  return (
    <CartTotals
      shippingPrice={shipping?.price ?? 0}
      showItemCount
      leadingRows={[
        { label: 'شماره موبایل', value: toPersianDigits(phone) },
        { label: 'روش ارسال', value: shipping?.title ?? '—' },
      ]}
    />
  );
}
