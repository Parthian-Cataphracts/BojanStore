import Link from 'next/link';
import type { ReactNode } from 'react';
import { Icon } from '@bojan/ui';
import { AdminTopBar } from './AdminTopBar';

export interface AdminCrumb {
  label: string;
  href?: string;
}

export interface AdminPageProps {
  title: string;
  description?: string;
  /** Trail above the title; the last item is the current page. */
  breadcrumbs?: AdminCrumb[];
  /** Buttons rendered on the far side of the title row. */
  actions?: ReactNode;
  children: ReactNode;
}

/**
 * Frame for every admin screen: fixed top bar, breadcrumb, title row with
 * actions, then the page body. `pt-[88px]` clears the 64px fixed bar.
 */
export function AdminPage({
  title,
  description,
  breadcrumbs,
  actions,
  children,
}: AdminPageProps) {
  return (
    <>
      <AdminTopBar title={title} />

      <main className="flex flex-col gap-lg p-lg pt-[88px]">
        {breadcrumbs && breadcrumbs.length > 0 && (
          <nav aria-label="مسیر صفحه" className="flex flex-wrap items-center gap-xs">
            {breadcrumbs.map((crumb, index) => {
              const isLast = index === breadcrumbs.length - 1;
              return (
                <span key={`${crumb.label}-${index}`} className="flex items-center gap-xs">
                  {crumb.href && !isLast ? (
                    <Link
                      href={crumb.href}
                      className="text-caption text-on-surface-variant transition-colors hover:text-primary"
                    >
                      {crumb.label}
                    </Link>
                  ) : (
                    <span
                      aria-current={isLast ? 'page' : undefined}
                      className={
                        isLast ? 'text-caption font-semibold text-primary' : 'text-caption text-on-surface-variant'
                      }
                    >
                      {crumb.label}
                    </span>
                  )}
                  {!isLast && (
                    <Icon name="chevron_left" size={16} className="text-outline-variant" />
                  )}
                </span>
              );
            })}
          </nav>
        )}

        <header className="flex flex-wrap items-start justify-between gap-md">
          <div className="flex min-w-0 flex-col gap-xs">
            {/* The top bar carries the h1 on desktop; this is the in-page title. */}
            <h2 className="font-headline text-card-title text-primary md:text-section-title">
              {title}
            </h2>
            {description && (
              <p className="max-w-3xl text-body-md leading-relaxed text-on-surface-variant">
                {description}
              </p>
            )}
          </div>

          {actions && <div className="flex shrink-0 flex-wrap gap-sm">{actions}</div>}
        </header>

        {children}
      </main>
    </>
  );
}
