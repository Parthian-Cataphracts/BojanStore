import type { ReactNode } from 'react';
import { cn } from '../lib/cn';
import { Icon } from './Icon';

export interface SectionHeaderProps {
  title: string;
  subtitle?: string;
  /** "View all" style link shown on the far side of the row. */
  actionLabel?: string;
  actionHref?: string;
  action?: ReactNode;
  className?: string;
}

/** The `section title … view all` row that opens every homepage section. */
export function SectionHeader({
  title,
  subtitle,
  actionLabel,
  actionHref,
  action,
  className,
}: SectionHeaderProps) {
  return (
    <div className={cn('flex items-end justify-between gap-md', className)}>
      <div className="flex flex-col gap-xs">
        <h2 className="font-headline text-display-md text-primary md:text-headline-lg">{title}</h2>
        {subtitle && <p className="text-caption text-on-surface-variant">{subtitle}</p>}
      </div>

      {action ??
        (actionLabel && actionHref && (
          <a
            href={actionHref}
            className="inline-flex shrink-0 items-center gap-xs text-label-md font-label-md text-secondary transition-colors hover:text-primary"
          >
            {actionLabel}
            <Icon name="chevron_left" size={18} />
          </a>
        ))}
    </div>
  );
}
