'use client';

import Link from 'next/link';
import { Card, Icon } from '@bojan/ui';
import { CartTotals } from './CartTotals';
import { useCheckout } from '@/lib/checkout/store';
import type { ShippingMethod } from '@/lib/mock/checkout';
import { routes } from '@/lib/routes';

/**
 * Resolves the shipping method the shopper actually picked.
 *
 * It lives in the checkout selection in `sessionStorage`, so anything that
 * shows it has to be client-side. Screen 79 reached for `shippingMethods[2]`
 * instead — the third method in whatever order the API returned them, named and
 * priced on the last screen before payment regardless of what was chosen on
 * screen 73, and disagreeing with screen 77 next door, which reached for
 * `shippingMethods[0]`. Both also asserted the index was present, which it need
 * not be: a store offering two delivery options, or an API call that failed and
 * returned `[]`, crashed the screen outright.
 */
function useChosenShipping(shippingMethods: ShippingMethod[]) {
  const { selection, hydrated } = useCheckout();
  const chosen = shippingMethods.find((method) => method.id === selection.shippingMethodId);

  return { chosen, hydrated };
}

/** Screen 79's delivery card. */
export function ChosenShippingCard({ shippingMethods }: { shippingMethods: ShippingMethod[] }) {
  const { chosen, hydrated } = useChosenShipping(shippingMethods);

  return (
    <Card className="flex flex-col gap-sm p-lg">
      <div className="flex items-center justify-between gap-md">
        <h2 className="flex items-center gap-xs text-label-md font-semibold text-primary">
          <Icon name="local_shipping" size={20} />
          نحوه ارسال
        </h2>
        <Link
          href={routes.checkoutShipping}
          className="text-label-md font-semibold text-secondary transition-colors hover:text-primary"
        >
          ویرایش
        </Link>
      </div>

      {hydrated && chosen ? (
        <span className="flex items-center gap-sm">
          <Icon name={chosen.icon} size={22} className="text-primary" />
          <span className="flex flex-col">
            <span className="text-body-md text-on-surface">{chosen.label}</span>
            {chosen.note && (
              <span className="text-caption text-on-surface-variant">{chosen.note}</span>
            )}
          </span>
        </span>
      ) : (
        // Nothing picked, or the selection is not read yet. The edit link
        // above is the way forward either way, so this says what is missing
        // rather than naming a method the shopper did not choose.
        <p className="text-body-md text-on-surface-variant">
          {hydrated ? 'هنوز روش ارسالی انتخاب نشده است.' : '—'}
        </p>
      )}
    </Card>
  );
}

/** Screen 79's totals, charging the chosen method's delivery fee. */
export function ChosenShippingTotals({
  shippingMethods,
  freeShippingThreshold = 0,
}: {
  shippingMethods: ShippingMethod[];
  /** The shop's free-delivery threshold, so this screen charges what the order will. */
  freeShippingThreshold?: number;
}) {
  const { chosen } = useChosenShipping(shippingMethods);

  return (
    <CartTotals shippingPrice={chosen?.price ?? 0} freeShippingThreshold={freeShippingThreshold} />
  );
}
