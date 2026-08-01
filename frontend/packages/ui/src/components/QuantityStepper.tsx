'use client';

import { cn } from '../lib/cn';
import { toPersianDigits } from '../lib/format';
import { Icon } from './Icon';

export interface QuantityStepperProps {
  value: number;
  onChange: (next: number) => void;
  min?: number;
  max?: number;
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
  disabled = false,
  className,
}: QuantityStepperProps) {
  const clamp = (next: number) => Math.min(max, Math.max(min, next));

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
        aria-label="کاهش تعداد"
        disabled={disabled || value <= min}
        onClick={() => onChange(clamp(value - 1))}
        className="flex h-8 w-8 items-center justify-center rounded-full text-primary transition-colors hover:bg-surface-container disabled:cursor-not-allowed disabled:text-outline-variant"
      >
        <Icon name="remove" size={18} />
      </button>

      <span className="tabular min-w-[2ch] text-center text-body-md font-label-md text-on-surface">
        {toPersianDigits(value)}
      </span>

      <button
        type="button"
        aria-label="افزایش تعداد"
        disabled={disabled || value >= max}
        onClick={() => onChange(clamp(value + 1))}
        className="flex h-8 w-8 items-center justify-center rounded-full text-primary transition-colors hover:bg-surface-container disabled:cursor-not-allowed disabled:text-outline-variant"
      >
        <Icon name="add" size={18} />
      </button>
    </div>
  );
}
