import type { ReactNode } from 'react';
import type { CheckoutShippingMethod } from '@/lib/api/cart';
import { Container } from '@/components/layout/Container';
import { CheckoutStepper, type CheckoutStepId } from './CheckoutStepper';
import { CheckoutSummaryRail, type SummaryRow } from './CheckoutSummaryRail';

export interface CheckoutShellProps {
  step: CheckoutStepId;
  title: string;
  description?: string;
  /**
   * Whether to show the totals rail. Steps that do not deal in money leave it
   * off.
   *
   * There is deliberately no `cart` prop: the basket lives in the browser, so a
   * server component has none to pass and every caller was passing the mock
   * one. The rail reads the real cart itself.
   */
  showSummary?: boolean;
  /** Extra rows merged into the totals. The shipping row is derived, not passed. */
  extraRows?: SummaryRow[];
  /** The shipping tiers on offer, so the rail can price the chosen one. */
  shippingMethods?: CheckoutShippingMethod[];
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
  showSummary = false,
  extraRows = [],
  shippingMethods = [],
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

      <div className={showSummary ? 'grid gap-lg lg:grid-cols-[1fr_340px] lg:items-start' : ''}>
        <div className="flex flex-col gap-lg">{children}</div>

        {showSummary && (
          <CheckoutSummaryRail
            extraRows={extraRows}
            shippingMethods={shippingMethods}
            {...(nextHref ? { nextHref } : null)}
            nextLabel={nextLabel}
            {...(backHref ? { backHref } : null)}
          />
        )}
      </div>
    </Container>
  );
}
