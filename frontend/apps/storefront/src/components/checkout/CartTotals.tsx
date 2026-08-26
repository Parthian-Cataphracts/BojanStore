'use client';

import type { ReactNode } from 'react';
import { Card, formatPrice, toPersianDigits } from '@bojan/ui';
import { useCart } from '@/lib/cart/store';

/**
 * Goods, discount, shipping and the payable total, for the checkout screens
 * that show the figures as a block rather than in the sticky rail (79 summary,
 * 80 confirm).
 *
 * The shipping cost is passed in because it is the *chosen* method's, which is
 * a decision the step owns; everything else comes from the basket. Both used to
 * compute a total from the mock basket, so the number on the last screen before
 * payment was not the number the order would charge.
 */
export function CartTotals({
  shippingPrice,
  shippingLabel = 'هزینه ارسال',
  freeShippingThreshold = null,
  leadingRows = [],
  showItemCount = false,
}: {
  shippingPrice: number;
  shippingLabel?: string;
  /**
   * What the goods have to come to for *the chosen method* to be free.
   *
   * Null when that method is never free. Per method rather than one figure for
   * the shop, so this screen charges exactly what the order will — the two used
   * to be separate numbers and only one of them was consulted when the money was
   * counted.
   */
  freeShippingThreshold?: number | null;
  /** Rows the step owns — order reference, phone, chosen method — shown first. */
  leadingRows?: { label: string; value: ReactNode }[];
  /** Adds a "تعداد کالاها" row counted from the basket rather than from a fixture. */
  showItemCount?: boolean;
}) {
  // The purchasable count, not every line: this row sits directly above a
  // subtotal that no longer includes sold-out items, and the two have to be
  // describing the same basket.
  const { cart, purchasableLines, hydrated } = useCart();
  const count = purchasableLines.length;

  const money = (value: number) => (hydrated ? formatPrice(value) : '—');

  // The same arithmetic the checkout does, against the goods total after any
  // coupon — so the figure here is the figure that will be charged. The shop
  // advertised free delivery on every product page and then charged for it
  // anyway; showing it here is half the repair, and applying it in
  // CheckoutService is the other half.
  const goods = Math.max(0, cart.subtotal - cart.discount);
  const earnedFreeShipping =
    typeof freeShippingThreshold === 'number' && goods >= freeShippingThreshold;
  const charged = earnedFreeShipping ? 0 : shippingPrice;

  const total = goods + charged;

  return (
    <Card className="flex flex-col gap-sm p-lg">
      <dl className="flex flex-col gap-sm text-body-md">
        {leadingRows.map((row) => (
          <div key={row.label} className="flex items-center justify-between gap-md">
            <dt className="text-on-surface-variant">{row.label}</dt>
            <dd className="tabular text-on-surface">{row.value}</dd>
          </div>
        ))}

        {showItemCount && (
          <div className="flex items-center justify-between gap-md">
            <dt className="text-on-surface-variant">تعداد کالاها</dt>
            <dd className="tabular text-on-surface">
              {hydrated ? `${toPersianDigits(count)} کالا` : '—'}
            </dd>
          </div>
        )}

        <div className="flex items-center justify-between">
          <dt className="text-on-surface-variant">جمع کالاها</dt>
          <dd className="tabular text-on-surface">{money(cart.subtotal)}</dd>
        </div>

        {/* Only when there is one — a "−۰ تومان" row reads like a mistake. */}
        {hydrated && cart.discount > 0 && (
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">تخفیف</dt>
            <dd className="tabular text-secondary">−{formatPrice(cart.discount)}</dd>
          </div>
        )}

        <div className="flex items-center justify-between">
          <dt className="text-on-surface-variant">{shippingLabel}</dt>
          {earnedFreeShipping ? (
            <dd className="flex items-center gap-xs">
              <span className="tabular text-caption text-outline line-through">
                {formatPrice(shippingPrice)}
              </span>
              <span className="text-secondary">رایگان</span>
            </dd>
          ) : (
            <dd className="tabular text-on-surface">{formatPrice(shippingPrice)}</dd>
          )}
        </div>

        {/* How much more would earn it. Shown only when it is actually within
            reach, because "spend another nine hundred thousand" is not an
            offer, it is a reproach. */}
        {hydrated &&
          !earnedFreeShipping &&
          typeof freeShippingThreshold === 'number' &&
          freeShippingThreshold > 0 &&
          goods >= freeShippingThreshold / 2 && (
          <p className="text-caption leading-relaxed text-on-surface-variant">
            {formatPrice(freeShippingThreshold - goods)} دیگر خرید کنید تا ارسال رایگان شود.
            </p>
          )}

        <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
          <dt className="text-body-lg font-semibold text-primary">مبلغ قابل پرداخت</dt>
          <dd className="tabular text-body-lg font-semibold text-primary">{money(total)}</dd>
        </div>
      </dl>
    </Card>
  );
}
