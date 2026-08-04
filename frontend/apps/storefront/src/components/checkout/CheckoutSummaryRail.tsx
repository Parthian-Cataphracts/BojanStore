'use client';

import Link from 'next/link';
import { Card, Icon, buttonClasses, formatPrice } from '@bojan/ui';
import { useCart } from '@/lib/cart/store';
import { useCheckout } from '@/lib/checkout/store';
import type { ShippingMethod } from '@/lib/mock/checkout';

export interface SummaryRow {
  label: string;
  value: string;
  tone?: 'default' | 'discount';
}

/**
 * The order-summary rail on the guided checkout screens (71-80).
 *
 * A client component because the basket is one: it lives in `localStorage`
 * until the cart endpoints exist, so a server-rendered page cannot see it. The
 * step screens used to be handed the mock basket instead, which meant every one
 * of them showed fixture items and a fixture total beside whatever the shopper
 * had actually chosen — the totals on screen disagreed with the order about to
 * be placed.
 *
 * Rows passed in by the step are merged into the totals.
 *
 * Shipping is not one of them. A step that hands in a price has to know which
 * method the shopper picked, and a server component cannot — so the three
 * screens that tried reached for a fixed index instead: 73 and 77 quoted
 * `shippingMethods[0]`, 79 quoted `shippingMethods[2]`, and the two disagreed
 * with each other and with the choice made on 73. Both also asserted the index
 * was there, which it need not be: `getShippingMethods` returns `[]` when the
 * API is unreachable, so an outage turned a checkout step into a crash rather
 * than a page missing one figure. The rail resolves the chosen method itself,
 * the way it already reads the basket itself, and the way screen 78's
 * `ConfirmSummary` already did.
 */
export function CheckoutSummaryRail({
  extraRows = [],
  shippingMethods,
  nextHref,
  nextLabel = 'ادامه',
  backHref,
}: {
  extraRows?: SummaryRow[];
  /** Supplied by steps whose totals include delivery; the chosen one is picked from it here. */
  shippingMethods?: ShippingMethod[];
  nextHref?: string;
  nextLabel?: string;
  backHref?: string;
}) {
  const { cart, hydrated } = useCart();
  const { selection, hydrated: selectionReady } = useCheckout();

  // Before hydration the store has no lines yet, and rendering zeros would
  // flash a wrong total. The rail keeps its shape so the layout does not jump.
  const pending = !hydrated;

  const money = (value: number) => (pending ? '—' : formatPrice(value));

  const chosenShipping = shippingMethods?.find(
    (method) => method.id === selection.shippingMethodId,
  );

  // Nothing chosen yet is the normal state on arriving at screen 73, so the row
  // shows an em dash rather than the first method's price, which would read as
  // a decision the shopper has not made.
  const shippingKnown = selectionReady && chosenShipping !== undefined;
  const shippingPrice = chosenShipping?.price ?? 0;

  // Where a method is in play this rail owns the arithmetic: `cart.total`
  // carries the store's default shipping fee, which is not necessarily the one
  // being charged here.
  const payable = shippingMethods ? cart.subtotal - cart.discount + shippingPrice : cart.total;

  return (
    <Card className="flex flex-col gap-md p-lg lg:sticky lg:top-24">
      <h2 className="font-headline text-card-title text-primary">خلاصه سفارش</h2>

      <dl className="flex flex-col gap-sm text-body-md">
        <div className="flex items-center justify-between">
          <dt className="text-on-surface-variant">جمع کالاها</dt>
          <dd className="tabular text-on-surface">{money(cart.subtotal)}</dd>
        </div>

        {!pending && cart.discount > 0 && (
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">تخفیف</dt>
            <dd className="tabular text-secondary">−{formatPrice(cart.discount)}</dd>
          </div>
        )}

        {shippingMethods && (
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">هزینه ارسال</dt>
            <dd className="tabular text-on-surface">
              {shippingKnown ? formatPrice(shippingPrice) : '—'}
            </dd>
          </div>
        )}

        {extraRows.map((row) => (
          <div key={row.label} className="flex items-center justify-between">
            <dt className="text-on-surface-variant">{row.label}</dt>
            <dd
              className={
                row.tone === 'discount' ? 'tabular text-secondary' : 'tabular text-on-surface'
              }
            >
              {row.value}
            </dd>
          </div>
        ))}

        <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
          <dt className="text-body-lg font-semibold text-primary">مبلغ قابل پرداخت</dt>
          <dd className="tabular text-body-lg font-semibold text-primary">{money(payable)}</dd>
        </div>
      </dl>

      {nextHref && (
        <Link href={nextHref} className={buttonClasses({ size: 'lg', fullWidth: true })}>
          {nextLabel}
        </Link>
      )}

      {backHref && (
        <Link
          href={backHref}
          className="flex items-center justify-center gap-xs text-label-md font-medium text-on-surface-variant transition-colors hover:text-primary"
        >
          <Icon name="arrow_forward" size={18} />
          مرحله قبل
        </Link>
      )}

      <p className="flex items-start gap-xs text-caption leading-relaxed text-on-surface-variant">
        <Icon name="lock" size={16} className="mt-px shrink-0" />
        پرداخت از طریق درگاه امن بانکی انجام می‌شود.
      </p>
    </Card>
  );
}
