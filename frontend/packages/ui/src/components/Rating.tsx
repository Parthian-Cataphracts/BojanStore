import { cn } from '../lib/cn';
import { toPersianDigits } from '../lib/format';
import { Icon } from './Icon';

export interface RatingProps {
  /** 0–5. */
  value: number;
  /** Number of reviews shown next to the stars. */
  count?: number;
  size?: number;
  /** Show a single star + numeric value instead of five stars. */
  compact?: boolean;
  className?: string;
}

export function Rating({ value, count, size = 16, compact = false, className }: RatingProps) {
  const rounded = Math.round(value * 10) / 10;
  const label = `${rounded} از ۵`;

  if (compact) {
    return (
      <span
        className={cn('inline-flex items-center gap-xs text-caption text-on-surface-variant', className)}
        aria-label={label}
      >
        <Icon name="star" filled size={size} className="text-secondary-container" />
        <span className="tabular">{toPersianDigits(rounded)}</span>
        {count !== undefined && <span className="tabular">({toPersianDigits(count)})</span>}
      </span>
    );
  }

  return (
    <span className={cn('inline-flex items-center gap-xs', className)} aria-label={label}>
      <span className="inline-flex" role="img">
        {[1, 2, 3, 4, 5].map((star) => (
          <Icon
            key={star}
            name="star"
            filled={star <= Math.round(value)}
            size={size}
            className={star <= Math.round(value) ? 'text-secondary-container' : 'text-outline-variant'}
          />
        ))}
      </span>
      {count !== undefined && (
        <span className="tabular text-caption text-on-surface-variant">
          ({toPersianDigits(count)} نظر)
        </span>
      )}
    </span>
  );
}
