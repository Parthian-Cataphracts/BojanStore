import Link from 'next/link';
import type { ReactNode } from 'react';
import { Icon } from '@bojan/ui';

export interface PageHeaderProps {
  title: string;
  /** Where the back arrow goes. Sub-pages in the design always have one. */
  backHref: string;
  subtitle?: string;
  /** Trailing control, e.g. a search or add button. */
  action?: ReactNode;
}

/**
 * Sub-page header from screens 11-16: back arrow, title, optional trailing
 * action. The arrow points forward (right) because the design is RTL.
 */
export function PageHeader({ title, backHref, subtitle, action }: PageHeaderProps) {
  return (
    <header className="mb-lg flex items-start justify-between gap-md">
      <div className="flex min-w-0 items-center gap-sm">
        <Link
          href={backHref}
          aria-label="بازگشت"
          className="shrink-0 text-primary transition-opacity hover:opacity-80 active:scale-95"
        >
          <Icon name="arrow_forward" />
        </Link>

        <div className="flex min-w-0 flex-col gap-xs">
          <h1 className="truncate font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
            {title}
          </h1>
          {subtitle && <p className="text-body-md text-on-surface-variant">{subtitle}</p>}
        </div>
      </div>

      {action && <div className="shrink-0">{action}</div>}
    </header>
  );
}
