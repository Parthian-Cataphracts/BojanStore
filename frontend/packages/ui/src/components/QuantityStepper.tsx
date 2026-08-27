'use client';

import { cn } from '../lib/cn';
import { toPersianDigits } from '../lib/format';
import { Icon } from './Icon';

export interface QuantityStepperProps {
  value: number;
  onChange: (next: number) => void;
  min?: number;
  max?: number;
  /**
   * Turns the decrement control into a remove control at `min`.
   *
   * Without it the minus simply goes dead at one, which is right where the
   * stepper stands beside its own delete button — the cart's rows do. Where the
   * stepper *is* the whole control, as on the product page, a dead button is
   * the only way back out of the basket and there is none: the shopper has to
   * go to the cart to undo a tap they made here. Given this, one is a trash
   * icon and pressing it takes the line out.
   */
  onRemove?: () => void;
  /**
   * Reports the *change* rather than the result, for a caller that would
   * rather apply it than compute it.
   *
   * `onChange` hands over `value + 1`, worked out from the value this render
   * was given. Two presses inside one frame are both worked out from the same
   * one, so they ask for the same number and the second is not a second press
   * at all — it is the first one repeated. A shopper tapping «+» quickly on a
   * product page got three of something after five taps. A delta cannot say
   * the wrong number, because it does not say a number: the store adds it to
   * whatever the line holds when the action arrives.
   */
  onStep?: (delta: number) => void;
  /** Disables the whole control, e.g. while a cart mutation is in flight. */
  disabled?: boolean;
  className?: string;
}

/** The decrement / count / increment pill used in the cart and on the product page. */
export function QuantityStepper({
  value,
  onChange,
  min = 1,
  max = 99,
  onRemove,
  onStep,
  disabled = false,
  className,
}: QuantityStepperProps) {
  const clamp = (next: number) => Math.min(max, Math.max(min, next));

  // Only at the floor, and only when there is somewhere for it to go. Above it
  // the button is an ordinary decrement, because taking one off a line of three
  // is not removing anything.
  const removes = onRemove !== undefined && value <= min;

  return (
    <div
      className={cn(
        'inline-flex items-center gap-sm rounded-full border border-outline-variant bg-surface-container-lowest px-xs',
        disabled && 'opacity-50',
        className,
      )}
    >
      <button
        type="button"
        aria-label={removes ? 'حذف از سبد خرید' : 'کاهش تعداد'}
        disabled={disabled || (!removes && value <= min)}
        onClick={() => {
          if (removes) onRemove();
          else if (onStep) onStep(-1);
          else onChange(clamp(value - 1));
        }}
        className={cn(
          'flex h-8 w-8 items-center justify-center rounded-full transition-colors disabled:cursor-not-allowed disabled:text-outline-variant',
          // Red, because it is the destructive one — and the only control here
          // whose effect the shopper cannot undo with the button beside it.
          removes
            ? 'text-error hover:bg-error-container'
            : 'text-primary hover:bg-surface-container',
        )}
      >
        <Icon name={removes ? 'delete' : 'remove'} size={18} />
      </button>

      <span className="tabular min-w-[2ch] text-center text-body-md font-label-md text-on-surface">
        {toPersianDigits(value)}
      </span>

      <button
        type="button"
        aria-label="افزایش تعداد"
        disabled={disabled || value >= max}
        onClick={() => (onStep ? onStep(1) : onChange(clamp(value + 1)))}
        className="flex h-8 w-8 items-center justify-center rounded-full text-primary transition-colors hover:bg-surface-container disabled:cursor-not-allowed disabled:text-outline-variant"
      >
        <Icon name="add" size={18} />
      </button>
    </div>
  );
}
