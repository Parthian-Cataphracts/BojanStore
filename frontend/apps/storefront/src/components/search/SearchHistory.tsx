'use client';

import Link from 'next/link';
import { Card, Icon } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { useBrowsing } from '@/lib/browsing/store';
import { routes } from '@/lib/routes';

/**
 * Screen 90 — recent search terms with per-item and bulk removal.
 *
 * The terms come from the browsing store, so this is the shopper's own
 * history: running a search adds to it, and clearing it here means the next
 * search screen starts empty.
 */
export function SearchHistory() {
  const { terms, hydrated, forgetTerm, clearTerms } = useBrowsing();

  function clearAll() {
    clearTerms();
    // Mirrored to the account for a signed-in shopper; a signed-out one gets a
    // 401 and keeps the local clear, which is the behaviour they asked for.
    void postJson('/api/account/search-history-clear', { all: true }).catch(() => {});
  }

  // Nothing to show before storage has been read, and nothing to show when the
  // shopper has no history — in both cases the section stays out of the way.
  if (!hydrated || terms.length === 0) return null;

  return (
    <section className="flex flex-col gap-md">
      <div className="flex items-center justify-between gap-md">
        <h2 className="text-label-md font-semibold text-primary">تاریخچه جستجو</h2>
        <button
          type="button"
          onClick={clearAll}
          className="text-label-md font-medium text-secondary transition-colors hover:text-primary"
        >
          پاک کردن همه
        </button>
      </div>

      <Card className="divide-y divide-paper-border">
        {terms.map((term) => (
          <div key={term} className="flex items-center gap-sm p-md">
            <Icon name="history" size={20} className="shrink-0 text-outline" />

            <Link
              href={`${routes.search}?q=${encodeURIComponent(term)}`}
              className="min-w-0 flex-1 truncate text-body-md text-on-surface transition-colors hover:text-primary"
            >
              {term}
            </Link>

            <button
              type="button"
              aria-label={`حذف ${term} از تاریخچه`}
              onClick={() => forgetTerm(term)}
              className="shrink-0 text-outline transition-colors hover:text-error"
            >
              <Icon name="close" size={18} />
            </button>
          </div>
        ))}
      </Card>
    </section>
  );
}
