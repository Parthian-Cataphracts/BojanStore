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
  leadingRows = [],
  showItemCount = false,
}: {
  shippingPrice: number;
  shippingLabel?: string;
  /** Rows the step owns — order reference, phone, chosen method — shown first. */
  leadingRows?: { label: string; value: ReactNode }[];
  /** Adds a "تعداد کالاها" row counted from the basket rather than from a fixture. */
  showItemCount?: boolean;
}) {
  const { cart, count, hydrated } = useCart();

  const money = (value: number) => (hydrated ? formatPrice(value) : '—');
  const total = cart.subtotal - cart.discount + shippingPrice;

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
          <dd className="tabular text-on-surface">{formatPrice(shippingPrice)}</dd>
        </div>

        <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
          <dt className="text-body-lg font-semibold text-primary">مبلغ قابل پرداخت</dt>
          <dd className="tabular text-body-lg font-semibold text-primary">{money(total)}</dd>
        </div>
      </dl>
    </Card>
  );
}
