import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { cn } from '../lib/cn';
import { Icon } from './Icon';

export type ButtonVariant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md' | 'lg';

const variants: Record<ButtonVariant, string> = {
  // Coral CTA — the design's main call to action.
  primary: 'bg-secondary-container text-on-tertiary hover:bg-secondary active:bg-secondary',
  // Deep teal — used for confirm/submit inside panels and the admin app.
  secondary:
    'bg-primary-container text-on-primary hover:bg-primary active:bg-primary',
  outline:
    'border border-primary text-primary bg-transparent hover:bg-primary hover:text-on-primary',
  ghost: 'bg-transparent text-on-surface-variant hover:bg-surface-container hover:text-primary',
  danger: 'bg-error text-on-error hover:bg-error/90',
};

const sizes: Record<ButtonSize, string> = {
  sm: 'h-9 px-md text-caption gap-xs',
  md: 'h-12 px-lg text-label-md gap-xs',
  lg: 'h-14 px-xl text-label-md gap-sm',
};

/**
 * Shared button styling, exported so anchors and `next/link` can look like
 * buttons without nesting an `<a>` inside a `<button>`.
 */
export function buttonClasses({
  variant = 'primary',
  size = 'md',
  fullWidth = false,
  className,
}: {
  variant?: ButtonVariant;
  size?: ButtonSize;
  fullWidth?: boolean;
  className?: string;
} = {}): string {
  return cn(
    'inline-flex items-center justify-center rounded-lg font-label-md transition-colors duration-200',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 focus-visible:ring-offset-background',
    'disabled:cursor-not-allowed disabled:opacity-50',
    variants[variant],
    sizes[size],
    fullWidth && 'w-full',
    className,
  );
}

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  /** Material Symbols name rendered before the label (i.e. to its right in RTL). */
  icon?: string;
  /** Material Symbols name rendered after the label. */
  trailingIcon?: string;
  fullWidth?: boolean;
  loading?: boolean;
  children?: ReactNode;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  {
    variant = 'primary',
    size = 'md',
    icon,
    trailingIcon,
    fullWidth = false,
    loading = false,
    disabled,
    className,
    children,
    ...props
  },
  ref,
) {
  return (
    <button
      ref={ref}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={buttonClasses({ variant, size, fullWidth, ...(className ? { className } : null) })}
      {...props}
    >
      {loading ? (
        <Icon name="progress_activity" size={20} className="animate-spin" />
      ) : (
        icon && <Icon name={icon} size={20} />
      )}
      {children}
      {trailingIcon && !loading && <Icon name={trailingIcon} size={20} />}
    </button>
  );
});
