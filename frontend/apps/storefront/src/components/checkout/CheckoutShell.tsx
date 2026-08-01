import Link from 'next/link';
import type { ReactNode } from 'react';
import { Card, Icon, buttonClasses, formatPrice } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { CheckoutStepper, type CheckoutStepId } from './CheckoutStepper';
import type { Cart } from '@/lib/api/types';

export interface CheckoutShellProps {
  step: CheckoutStepId;
  title: string;
  description?: string;
  /** Cart totals rail; omitted on steps that do not show money. */
  cart?: Cart;
  /** Extra rows merged into the totals, e.g. the chosen shipping cost. */
  extraRows?: { label: string; value: string; tone?: 'default' | 'discount' }[];
  /** Primary action rendered under the totals. */
  nextHref?: string;
  nextLabel?: string;
  backHref?: string;
  children: ReactNode;
}

/**
 * Shared frame for the guided checkout screens (71-80): stepper, page title,
 * the step's own content, and the sticky order-summary rail on desktop.
 */
export function CheckoutShell({
  step,
  title,
  description,
  cart,
  extraRows = [],
  nextHref,
  nextLabel = 'ادامه',
  backHref,
  children,
}: CheckoutShellProps) {
  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <CheckoutStepper current={step} />

      <header className="flex flex-col gap-sm">
        <h1 className="font-headline text-headline-lg-mobile text-primary md:text-page-title">
          {title}
        </h1>
        {description && (
          <p className="max-w-2xl text-body-md leading-loose text-on-surface-variant">
            {description}
          </p>
        )}
      </header>

      <div className={cart ? 'grid gap-lg lg:grid-cols-[1fr_340px] lg:items-start' : ''}>
        <div className="flex flex-col gap-lg">{children}</div>

        {cart && (
          <Card className="flex flex-col gap-md p-lg lg:sticky lg:top-24">
            <h2 className="font-headline text-card-title text-primary">خلاصه سفارش</h2>

            <dl className="flex flex-col gap-sm text-body-md">
              <div className="flex items-center justify-between">
                <dt className="text-on-surface-variant">جمع کالاها</dt>
                <dd className="tabular text-on-surface">{formatPrice(cart.subtotal)}</dd>
              </div>

              {cart.discount > 0 && (
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
                <dd className="tabular text-body-lg font-semibold text-primary">
                  {formatPrice(cart.total)}
                </dd>
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
        )}
      </div>
    </Container>
  );
}
