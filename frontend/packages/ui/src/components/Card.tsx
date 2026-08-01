import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '../lib/cn';

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /**
   * `paper` — the warm bordered surface used for most content blocks.
   * `plain` — a flat white surface, used inside dense lists and tables.
   */
  surface?: 'paper' | 'plain';
  shadow?: 'none' | 'ambient' | 'soft';
  children?: ReactNode;
}

export function Card({
  surface = 'paper',
  shadow = 'ambient',
  className,
  children,
  ...props
}: CardProps) {
  return (
    <div
      className={cn(
        'rounded-xl',
        surface === 'paper' ? 'paper-card' : 'border border-outline-variant/40 bg-surface-container-lowest',
        shadow === 'ambient' && 'shadow-ambient',
        shadow === 'soft' && 'shadow-soft',
        className,
      )}
      {...props}
    >
      {children}
    </div>
  );
}

export function CardHeader({ className, children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        'flex items-center justify-between gap-md border-b border-paper-border px-lg py-md',
        className,
      )}
      {...props}
    >
      {children}
    </div>
  );
}

export function CardBody({ className, children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cn('p-lg', className)} {...props}>
      {children}
    </div>
  );
}

export function CardFooter({ className, children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn('flex items-center gap-md border-t border-paper-border px-lg py-md', className)}
      {...props}
    >
      {children}
    </div>
  );
}
