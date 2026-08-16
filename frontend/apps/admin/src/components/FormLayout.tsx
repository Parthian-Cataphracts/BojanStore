import type { ReactNode } from 'react';
import { Card, Icon, cn } from '@bojan/ui';

/** One titled section of an admin form. */
export function FormSection({
  title,
  description,
  icon,
  className,
  children,
}: {
  title: string;
  description?: string;
  icon?: string;
  /** For a section rendered outside `FormLayout`, which has no column to size it. */
  className?: string;
  children: ReactNode;
}) {
  return (
    <Card className={cn('gap-lg p-lg flex flex-col', className)}>
      <div className="gap-xs flex flex-col">
        <h3 className="gap-sm font-headline text-card-title text-primary flex items-center">
          {icon && <Icon name={icon} size={22} />}
          {title}
        </h3>
        {description && (
          <p className="text-caption text-on-surface-variant leading-relaxed">{description}</p>
        )}
      </div>

      {children}
    </Card>
  );
}

/**
 * Two-column admin form: sections on the left, a sticky sidebar (status,
 * publishing, media) on the right. Collapses to one column below `lg`.
 */
export function FormLayout({
  children,
  aside,
  actions,
}: {
  children: ReactNode;
  aside?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <div className="gap-lg flex flex-col">
      <div className={aside ? 'gap-lg grid lg:grid-cols-[1fr_320px] lg:items-start' : ''}>
        {/* `min-w-0` on the main column, because a grid item defaults to
            `min-width: auto` and so refuses to shrink below its widest child.
            Every form that puts a table or a long unbroken string in here would
            otherwise stretch the whole page sideways rather than scroll. */}
        <div className="gap-lg flex min-w-0 flex-col">{children}</div>
        {aside && <div className="gap-lg flex flex-col lg:sticky lg:top-24">{aside}</div>}
      </div>

      {actions && (
        <div className="gap-md border-outline-variant/40 pt-lg flex flex-wrap border-t">
          {actions}
        </div>
      )}
    </div>
  );
}
