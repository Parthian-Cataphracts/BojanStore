import Link from 'next/link';
import { Icon, cn, toPersianDigits } from '@bojan/ui';

export interface TablePaginationProps {
  page: number;
  pageSize: number;
  total: number;
  /** Current search params, so a page link keeps the active filters. */
  params: Record<string, string | string[] | undefined>;
  basePath: string;
}

/**
 * Pager for the admin tables.
 *
 * Links rather than buttons, for the same reason the listing filters are in the
 * URL: an operator can bookmark or share "page 3 of pending orders", and the
 * back button behaves.
 */
export function TablePagination({
  page,
  pageSize,
  total,
  params,
  basePath,
}: TablePaginationProps) {
  const last = Math.max(1, Math.ceil(total / pageSize));
  if (last <= 1) return null;

  const current = Math.min(Math.max(1, page), last);
  const from = (current - 1) * pageSize + 1;
  const to = Math.min(current * pageSize, total);

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

    if (target > 1) search.set('page', String(target));

    const query = search.toString();
    return query ? `${basePath}?${query}` : basePath;
  }

  const step =
    'flex h-9 min-w-9 items-center justify-center rounded-lg px-sm text-label-md font-label-md transition-colors';
  const enabled = 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary';

  return (
    <nav
      aria-label="صفحه‌بندی جدول"
      className="flex flex-wrap items-center justify-between gap-md pt-sm"
    >
      <p className="tabular text-caption text-on-surface-variant">
        نمایش {toPersianDigits(from)} تا {toPersianDigits(to)} از {toPersianDigits(total)} رکورد
      </p>

      <div className="flex items-center gap-xs">
        {current > 1 ? (
          <Link href={hrefFor(current - 1)} rel="prev" aria-label="صفحه قبل" className={cn(step, enabled)}>
            <Icon name="chevron_right" size={20} />
          </Link>
        ) : (
          <span aria-hidden="true" className={cn(step, 'text-outline-variant')}>
            <Icon name="chevron_right" size={20} />
          </span>
        )}

        <span className="tabular px-sm text-caption text-on-surface-variant">
          صفحه {toPersianDigits(current)} از {toPersianDigits(last)}
        </span>

        {current < last ? (
          <Link href={hrefFor(current + 1)} rel="next" aria-label="صفحه بعد" className={cn(step, enabled)}>
            <Icon name="chevron_left" size={20} />
          </Link>
        ) : (
          <span aria-hidden="true" className={cn(step, 'text-outline-variant')}>
            <Icon name="chevron_left" size={20} />
          </span>
        )}
      </div>
    </nav>
  );
}
