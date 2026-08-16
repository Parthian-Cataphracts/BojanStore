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
export function AdminPage({ title, description, breadcrumbs, actions, children }: AdminPageProps) {
  return (
    <>
      <AdminTopBar title={title} />

      {/*
        `gap-lg` separates the page's sections; the breadcrumb and the title are
        not two sections but two lines of one, so they sit in their own block
        with `gap-sm` between them. They used to be 24px apart, which read as a
        trail that belonged to the page above this one.
      */}
      <main className="gap-lg p-lg flex flex-col pt-[88px]">
        <div className="gap-sm flex flex-col">
          {breadcrumbs && breadcrumbs.length > 0 && (
            <nav aria-label="مسیر صفحه" className="gap-xs flex flex-wrap items-center">
              {breadcrumbs.map((crumb, index) => {
                const isLast = index === breadcrumbs.length - 1;
                return (
                  <span key={`${crumb.label}-${index}`} className="gap-xs flex items-center">
                    {crumb.href && !isLast ? (
                      <Link
                        href={crumb.href}
                        className="text-caption text-on-surface-variant hover:text-primary transition-colors"
                      >
                        {crumb.label}
                      </Link>
                    ) : (
                      <span
                        aria-current={isLast ? 'page' : undefined}
                        className={
                          isLast
                            ? 'text-caption text-primary font-semibold'
                            : 'text-caption text-on-surface-variant'
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

          {/*
          One header shape for all ninety screens: title and description on the
          reading side, actions on the other, a rule underneath. The rule is
          what makes a page with no KPI row still look like it has a header
          rather than like a title that happened to be followed by a table.

          `items-end` rather than `items-start`, so a page whose description
          runs to two lines keeps its buttons on the baseline of the block
          instead of floating beside the first line of it.
        */}
          <header className="gap-md border-outline-variant/60 pb-lg flex flex-wrap items-end justify-between border-b">
            <div className="gap-xs flex min-w-0 flex-col">
              {/* The top bar carries the h1 on desktop; this is the in-page title. */}
              <h2 className="font-headline text-section-title text-primary">
                {title}
              </h2>
              {description && (
                <p className="text-body-md text-on-surface-variant max-w-3xl leading-relaxed">
                  {description}
                </p>
              )}
            </div>

            {actions && (
              <div className="gap-sm flex shrink-0 flex-wrap items-center">{actions}</div>
            )}
          </header>
        </div>

        {children}
      </main>
    </>
  );
}
