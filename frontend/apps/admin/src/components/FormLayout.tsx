import type { ReactNode } from 'react';
import { Card, Icon } from '@bojan/ui';

/** One titled section of an admin form. */
export function FormSection({
  title,
  description,
  icon,
  children,
}: {
  title: string;
  description?: string;
  icon?: string;
  children: ReactNode;
}) {
  return (
    <Card className="flex flex-col gap-lg p-lg">
      <div className="flex flex-col gap-xs">
        <h3 className="flex items-center gap-sm font-headline text-card-title text-primary">
          {icon && <Icon name={icon} size={22} />}
          {title}
        </h3>
        {description && (
          <p className="text-caption leading-relaxed text-on-surface-variant">{description}</p>
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
    <div className="flex flex-col gap-lg">
      <div className={aside ? 'grid gap-lg lg:grid-cols-[1fr_320px] lg:items-start' : ''}>
        <div className="flex flex-col gap-lg">{children}</div>
        {aside && <div className="flex flex-col gap-lg lg:sticky lg:top-24">{aside}</div>}
      </div>

      {actions && (
        <div className="flex flex-wrap gap-md border-t border-outline-variant/40 pt-lg">
          {actions}
        </div>
      )}
    </div>
  );
}
