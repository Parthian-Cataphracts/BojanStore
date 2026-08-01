import { forwardRef, type ButtonHTMLAttributes } from 'react';
import { cn } from '../lib/cn';
import { Icon } from './Icon';

export interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  icon: string;
  /** Required — icon-only controls need an accessible name. */
  label: string;
  filled?: boolean;
  size?: number;
  variant?: 'plain' | 'surface' | 'outline';
}

const variants = {
  plain: 'text-on-surface-variant hover:text-primary',
  surface: 'bg-surface-container-low text-primary hover:bg-primary hover:text-on-primary',
  outline: 'border border-primary text-primary hover:bg-primary hover:text-on-primary',
} as const;

export const IconButton = forwardRef<HTMLButtonElement, IconButtonProps>(function IconButton(
  { icon, label, filled, size = 24, variant = 'plain', className, ...props },
  ref,
) {
  return (
    <button
      ref={ref}
      type="button"
      aria-label={label}
      title={label}
      className={cn(
        'inline-flex items-center justify-center rounded-full p-sm transition-colors duration-200',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary',
        'disabled:cursor-not-allowed disabled:opacity-50',
        variants[variant],
        className,
      )}
      {...props}
    >
      <Icon name={icon} filled={filled} size={size} />
    </button>
  );
});
