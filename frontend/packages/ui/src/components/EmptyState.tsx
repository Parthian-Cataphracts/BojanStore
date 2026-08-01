import type { ReactNode } from 'react';
import { cn } from '../lib/cn';
import { Icon } from './Icon';

export interface EmptyStateProps {
  icon?: string;
  title: string;
  description?: ReactNode;
  /** Primary action, e.g. a "start shopping" button. */
  action?: ReactNode;
  className?: string;
}

export function EmptyState({
  icon = 'inbox',
  title,
  description,
  action,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-md rounded-xl px-lg py-xl text-center',
        className,
      )}
    >
      <span className="flex h-20 w-20 items-center justify-center rounded-full bg-soft-mint text-primary">
        <Icon name={icon} size={36} />
      </span>
      <h3 className="font-headline text-display-md text-primary">{title}</h3>
      {description && (
        <p className="max-w-md text-body-md leading-relaxed text-on-surface-variant">
          {description}
        </p>
      )}
      {action && <div className="mt-sm">{action}</div>}
    </div>
  );
}
