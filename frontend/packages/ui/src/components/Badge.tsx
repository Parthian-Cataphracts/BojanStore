import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '../lib/cn';

export type BadgeTone = 'coral' | 'teal' | 'mint' | 'neutral' | 'success' | 'warning' | 'error';

const tones: Record<BadgeTone, string> = {
  coral: 'bg-secondary-container text-on-tertiary',
  teal: 'bg-primary-container text-on-primary',
  mint: 'bg-soft-mint text-primary',
  neutral: 'bg-surface-container-high text-on-surface-variant',
  success: 'bg-tertiary-fixed text-on-tertiary-fixed',
  warning: 'bg-secondary-fixed text-on-secondary-fixed-variant',
  error: 'bg-error-container text-on-error-container',
};

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: BadgeTone;
  children?: ReactNode;
}

export function Badge({ tone = 'neutral', className, children, ...props }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-xs rounded-full px-md py-xs text-caption font-label-md',
        tones[tone],
        className,
      )}
      {...props}
    >
      {children}
    </span>
  );
}
