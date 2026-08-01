import Link from 'next/link';
import { Icon, cn, toPersianDigits } from '@bojan/ui';

export interface PaginationProps {
  page: number;
  pageSize: number;
  total: number;
  /** Current search params, so a page link keeps the active filters. */
  params: Record<string, string | string[] | undefined>;
  /** Path the links point at — the listing this pager belongs to. */
  basePath: string;
}

/** Windowed page numbers with ellipses, so a long catalogue stays one row. */
function pageNumbers(current: number, last: number): (number | 'gap')[] {
  if (last <= 7) return Array.from({ length: last }, (_, index) => index + 1);

  const pages = new Set<number>([1, last, current]);
  if (current > 1) pages.add(current - 1);
  if (current < last) pages.add(current + 1);

  const sorted = [...pages].sort((a, b) => a - b);
  const out: (number | 'gap')[] = [];

  for (const [index, value] of sorted.entries()) {
    const previous = sorted[index - 1];
    if (previous !== undefined && value - previous > 1) out.push('gap');
    out.push(value);
  }

  return out;
}

/**
 * Listing pager.
 *
 * Server-rendered links rather than buttons: filter and sort state already
 * lives in the URL on these screens, so the page belongs there too — a paged
 * listing stays shareable and the back button keeps working.
 */
export function Pagination({ page, pageSize, total, params, basePath }: PaginationProps) {
  const last = Math.max(1, Math.ceil(total / pageSize));
  if (last <= 1) return null;

  const current = Math.min(Math.max(1, page), last);

  function hrefFor(target: number): string {
    const search = new URLSearchParams();

    for (const [key, value] of Object.entries(params)) {
      if (key === 'page' || value === undefined) continue;
      if (Array.isArray(value)) {
        for (const entry of value) search.append(key, entry);
      } else {
        search.set(key, value);
      }
    }

    // Page one is the bare URL — no `?page=1` to duplicate a canonical listing.
    if (target > 1) search.set('page', String(target));

    const query = search.toString();
    return query ? `${basePath}?${query}` : basePath;
  }

  const linkClasses =
    'flex h-10 min-w-10 items-center justify-center rounded-lg px-sm text-label-md font-label-md transition-colors';

  return (
    <nav aria-label="صفحه‌بندی" className="flex flex-wrap items-center justify-center gap-xs">
      {current > 1 ? (
        <Link
          href={hrefFor(current - 1)}
          rel="prev"
          aria-label="صفحه قبل"
          className={cn(linkClasses, 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary')}
        >
          <Icon name="chevron_right" size={20} />
        </Link>
      ) : (
        <span aria-hidden="true" className={cn(linkClasses, 'text-outline-variant')}>
          <Icon name="chevron_right" size={20} />
        </span>
      )}

      {pageNumbers(current, last).map((entry, index) =>
        entry === 'gap' ? (
          <span
            key={`gap-${index}`}
            aria-hidden="true"
            className="flex h-10 w-6 items-center justify-center text-outline"
          >
            …
          </span>
        ) : (
          <Link
            key={entry}
            href={hrefFor(entry)}
            aria-current={entry === current ? 'page' : undefined}
            aria-label={`صفحه ${toPersianDigits(entry)}`}
            className={cn(
              linkClasses,
              'tabular',
              entry === current
                ? 'bg-primary text-on-primary'
                : 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary',
            )}
          >
            {toPersianDigits(entry)}
          </Link>
        ),
      )}

      {current < last ? (
        <Link
          href={hrefFor(current + 1)}
          rel="next"
          aria-label="صفحه بعد"
          className={cn(linkClasses, 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary')}
        >
          <Icon name="chevron_left" size={20} />
        </Link>
      ) : (
        <span aria-hidden="true" className={cn(linkClasses, 'text-outline-variant')}>
          <Icon name="chevron_left" size={20} />
        </span>
      )}
    </nav>
  );
}
