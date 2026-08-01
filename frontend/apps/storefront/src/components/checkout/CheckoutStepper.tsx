import { Icon, cn, toPersianDigits } from '@bojan/ui';

/** The checkout steps as ordered in design screens 71-79. */
export const checkoutSteps = [
  { id: 'cart', label: 'سبد خرید' },
  { id: 'shipping', label: 'اطلاعات ارسال' },
  { id: 'payment', label: 'پرداخت' },
] as const;

export type CheckoutStepId = (typeof checkoutSteps)[number]['id'];

/**
 * Three-dot progress rail shared by every checkout step screen.
 * Completed steps show a check, the active one shows its number.
 */
export function CheckoutStepper({ current }: { current: CheckoutStepId }) {
  const activeIndex = checkoutSteps.findIndex((step) => step.id === current);

  return (
    <nav aria-label="مراحل خرید" className="relative flex items-start justify-between px-sm py-lg">
      {/* Rail behind the dots. */}
      <span
        aria-hidden="true"
        className="absolute inset-x-[12%] top-[34px] h-0.5 bg-outline-variant"
      />
      <span
        aria-hidden="true"
        className="absolute top-[34px] h-0.5 bg-primary transition-all"
        style={{
          insetInlineStart: '12%',
          width: `${(activeIndex / (checkoutSteps.length - 1)) * 76}%`,
        }}
      />

      {checkoutSteps.map((step, index) => {
        const done = index < activeIndex;
        const active = index === activeIndex;

        return (
          <span key={step.id} className="relative z-10 flex flex-1 flex-col items-center gap-sm">
            <span
              aria-current={active ? 'step' : undefined}
              className={cn(
                'flex h-9 w-9 items-center justify-center rounded-full border-2 text-label-md transition-colors',
                done && 'border-primary bg-primary text-on-primary',
                active && 'border-primary bg-soft-mint text-primary',
                !done && !active && 'border-outline-variant bg-surface text-outline-variant',
              )}
            >
              {done ? <Icon name="check" size={18} /> : toPersianDigits(index + 1)}
            </span>

            <span
              className={cn(
                'text-center text-caption',
                done || active ? 'font-semibold text-primary' : 'text-on-surface-variant',
              )}
            >
              {step.label}
            </span>
          </span>
        );
      })}
    </nav>
  );
}
