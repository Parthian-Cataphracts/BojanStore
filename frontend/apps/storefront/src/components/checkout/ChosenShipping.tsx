'use client';

import { Icon } from '@bojan/ui';
import type { CheckoutShippingMethod } from '@/lib/api/cart';
import { useCheckoutSelection } from '@/lib/checkout/store';
import { CartTotals } from './CartTotals';

/** The chosen tier, or the first one when the shopper has not reached that step. */
function useChosen(shippingMethods: CheckoutShippingMethod[]): CheckoutShippingMethod | undefined {
  const { selection } = useCheckoutSelection();
  return (
    shippingMethods.find((method) => method.id === selection.shippingMethodId) ??
    shippingMethods[0]
  );
}

/**
 * The shipping tier the shopper picked, named and described.
 *
 * Screens 79 and 80 each hard-coded an entry from the fixture list — screen 79
 * took the third one — so the "نحوه ارسال" they showed was whatever that index
 * happened to hold rather than the tier being paid for.
 */
export function ChosenShippingLine({
  shippingMethods,
}: {
  shippingMethods: CheckoutShippingMethod[];
}) {
  const chosen = useChosen(shippingMethods);

  if (!chosen) return null;

  return (
    <span className="flex items-center gap-sm">
      <Icon name={chosen.icon} size={22} className="text-primary" />
      <span className="flex flex-col">
        <span className="text-body-md text-on-surface">{chosen.title}</span>
        {chosen.note && <span className="text-caption text-on-surface-variant">{chosen.note}</span>}
      </span>
    </span>
  );
}

/** Screen 79's totals, priced against that same tier. */
export function ChosenShippingTotals({
  shippingMethods,
}: {
  shippingMethods: CheckoutShippingMethod[];
}) {
  const chosen = useChosen(shippingMethods);
  return <CartTotals shippingPrice={chosen?.price ?? 0} />;
}
