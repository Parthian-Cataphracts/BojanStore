import { Fragment } from 'react';
import { cn } from '../lib/cn';
import { Icon } from './Icon';

export interface Crumb {
  label: string;
  href?: string;
}

export interface BreadcrumbProps {
  items: Crumb[];
  className?: string;
}

/** RTL breadcrumb — the chevron points left, following the reading direction. */
export function Breadcrumb({ items, className }: BreadcrumbProps) {
  return (
    <nav aria-label="مسیر صفحه" className={cn('flex items-center gap-xs', className)}>
      {items.map((item, index) => {
        const isLast = index === items.length - 1;
        return (
          <Fragment key={`${item.label}-${index}`}>
            {item.href && !isLast ? (
              <a
                href={item.href}
                className="text-caption text-on-surface-variant transition-colors hover:text-primary"
              >
                {item.label}
              </a>
            ) : (
              <span
                aria-current={isLast ? 'page' : undefined}
                className={cn(
                  'text-caption',
                  isLast ? 'font-label-md text-primary' : 'text-on-surface-variant',
                )}
              >
                {item.label}
              </span>
            )}
            {!isLast && <Icon name="chevron_left" size={16} className="text-outline-variant" />}
          </Fragment>
        );
      })}
    </nav>
  );
}
