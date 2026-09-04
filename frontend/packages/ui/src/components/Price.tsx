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
  /**
   * Print «از» before the figure — a starting price rather than the price.
   *
   * For a product whose combinations are priced separately: the card shows the
   * cheapest one, and without this it reads as what the product costs when the
   * size the shopper wants may cost more.
   */
  from?: boolean;
  className?: string;
}

const sizes = {
  sm: 'text-caption',
  md: 'text-body-md',
  lg: 'text-display-md',
} as const;

export function Price({
  value,
  compareAt,
  size = 'md',
  showDiscount = true,
  from = false,
  className,
}: PriceProps) {
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
        {from && <span className="text-on-surface-variant font-normal">از </span>}
        {formatPrice(value)}
      </span>
    </span>
  );
}
