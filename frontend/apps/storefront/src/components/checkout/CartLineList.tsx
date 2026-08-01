'use client';

import Image from 'next/image';
import { Card, Icon, formatPrice, toPersianDigits } from '@bojan/ui';
import { useCart } from '@/lib/cart/store';

/**
 * The basket's own lines, for the checkout steps that recap what is being
 * bought (74 review, 75 summary).
 *
 * A client component for the same reason `CheckoutSummaryRail` is: the basket
 * lives in `localStorage`, so a server component cannot read it. These screens
 * used to map over the mock basket, which meant the recap listed fixture
 * products rather than the shopper's.
 */
export function CartLineList() {
  const { cart, hydrated } = useCart();

  if (!hydrated) {
    return (
      <Card className="flex items-center justify-center gap-sm p-xl text-body-md text-on-surface-variant">
        <Icon name="progress_activity" size={20} />
        در حال بارگذاری سبد خرید…
      </Card>
    );
  }

  if (cart.lines.length === 0) {
    return (
      <Card className="flex flex-col items-center gap-xs p-xl text-center">
        <Icon name="shopping_cart" size={32} className="text-outline" />
        <p className="text-body-md text-on-surface-variant">سبد خرید شما خالی است.</p>
      </Card>
    );
  }

  return (
    <Card className="divide-y divide-paper-border">
      {cart.lines.map((line) => (
        <div key={line.id} className="flex items-center gap-md p-md">
          <span className="relative h-16 w-16 shrink-0 overflow-hidden rounded border border-outline-variant">
            <Image src={line.image} alt={line.title} fill sizes="64px" className="object-cover" />
          </span>
          <span className="flex min-w-0 flex-1 flex-col gap-xs">
            <span className="line-clamp-2 text-body-md text-on-surface">{line.title}</span>
            <span className="tabular text-caption text-on-surface-variant">
              تعداد: {toPersianDigits(line.quantity)}
            </span>
          </span>
          <span className="tabular shrink-0 text-label-md font-semibold text-primary">
            {formatPrice(line.unitPrice * line.quantity)}
          </span>
        </div>
      ))}
    </Card>
  );
}
