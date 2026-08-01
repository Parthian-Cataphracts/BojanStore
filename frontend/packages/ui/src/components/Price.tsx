import { cn } from '../lib/cn';
import { formatPercent, formatPrice } from '../lib/format';

export interface PriceProps {
  /** Current selling price in Toman. */
  value: number;
  /** Original price, shown struck through when it is higher than `value`. */
  compareAt?: number;
  size?: 'sm' | 'md' | 'lg';
  /** Render the discount percentage badge next to the price. */
  showDiscount?: boolean;
  className?: string;
}

const sizes = {
  sm: 'text-caption',
  md: 'text-body-md',
  lg: 'text-display-md',
} as const;

export function Price({ value, compareAt, size = 'md', showDiscount = true, className }: PriceProps) {
  const discounted = compareAt !== undefined && compareAt > value;
  const percent = discounted ? Math.round(((compareAt - value) / compareAt) * 100) : 0;

  return (
    <span className={cn('inline-flex flex-wrap items-center gap-xs', className)}>
      {discounted && showDiscount && (
        <span className="rounded-full bg-secondary-container px-xs py-px text-caption font-label-md text-on-tertiary tabular">
          {formatPercent(percent)}
        </span>
      )}
      {discounted && (
        <s className="tabular text-caption text-outline decoration-outline">
          {formatPrice(compareAt, { withUnit: false })}
        </s>
      )}
      <span className={cn('tabular font-label-md text-primary', sizes[size])}>
        {formatPrice(value)}
      </span>
    </span>
  );
}
