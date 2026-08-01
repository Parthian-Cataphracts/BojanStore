'use client';

import Link from 'next/link';
import { Card, Icon, buttonClasses, formatPrice } from '@bojan/ui';
import { useCart } from '@/lib/cart/store';

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
 * Rows passed in by the step (a chosen shipping cost, say) are merged into the
 * totals; the shipping the store itself derives is shown only when the step
 * does not supply its own, so the same figure never appears twice.
 */
export function CheckoutSummaryRail({
  extraRows = [],
  nextHref,
  nextLabel = 'ادامه',
  backHref,
}: {
  extraRows?: SummaryRow[];
  nextHref?: string;
  nextLabel?: string;
  backHref?: string;
}) {
  const { cart, hydrated } = useCart();

  // Before hydration the store has no lines yet, and rendering zeros would
  // flash a wrong total. The rail keeps its shape so the layout does not jump.
  const pending = !hydrated;

  const money = (value: number) => (pending ? '—' : formatPrice(value));

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
          <dd className="tabular text-body-lg font-semibold text-primary">{money(cart.total)}</dd>
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
