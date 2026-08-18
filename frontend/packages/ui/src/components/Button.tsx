import { forwardRef, useId, type ButtonHTMLAttributes, type ReactNode } from 'react';
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
  sm: 'h-9 text-caption gap-xs',
  md: 'h-12 text-label-md gap-xs',
  lg: 'h-14 text-label-md gap-sm',
};

/**
 * Side padding — what sets a button's width while it is sizing to its label.
 *
 * Separate from the size above because it stops applying once the button is
 * full-width: there the width comes from the container and the label is
 * centred in it, so the padding no longer positions anything. All it still
 * does is decide how narrow the label's box is, and `px-xl` on a phone made
 * that box narrower than the label — «افزودن به سبد خرید» broke across two
 * lines inside a 226px button with 80px of padding it was not using.
 */
const padding: Record<ButtonSize, string> = {
  sm: 'px-md',
  md: 'px-lg',
  lg: 'px-xl',
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
    // One padding class or the other, never both: they are the same property,
    // so which one won would be decided by the order Tailwind happened to emit
    // them in rather than by anything written here.
    fullWidth ? 'w-full px-md' : padding[size],
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
  /**
   * Visible explanation rendered beneath the button.
   *
   * For controls that are disabled for a reason worth stating — a feature the
   * server does not implement yet, a payment gateway that is not live. Those
   * used to say so in `title` alone, which is a tooltip: there is no hover on a
   * phone, and this is a phone-first shop, so most people saw a greyed-out
   * button and no reason at all. `Input` has always had this; a button that is
   * off is no less in need of an explanation than a field that is.
   */
  hint?: ReactNode;
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
    hint,
    className,
    children,
    ...props
  },
  ref,
) {
  const hintId = useId();

  const button = (
    <button
      ref={ref}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      aria-describedby={hint ? hintId : undefined}
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

  // Returned bare when there is no hint, so every existing call site keeps the
  // layout it has — a button is placed in flex rows and grids all over both
  // apps, and silently wrapping it in a div would move a great many of them.
  if (!hint) return button;

  return (
    <span className={cn('inline-flex flex-col gap-xs', fullWidth && 'w-full')}>
      {button}
      <span id={hintId} className="text-caption text-on-surface-variant">
        {hint}
      </span>
    </span>
  );
});
