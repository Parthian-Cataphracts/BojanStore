'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Icon, cn } from '@bojan/ui';

export interface FilterOption {
  value: string;
  label: string;
}

export interface FilterBarProps {
  searchPlaceholder?: string;
  /** Chip groups; each writes its own query param. */
  filters?: { param: string; label: string; options: FilterOption[] }[];
}

/**
 * Search box plus chip filters for the admin list screens.
 *
 * State lives in the URL so a filtered list is shareable and survives a reload
 * — the same contract the storefront catalogue uses.
 */
export function FilterBar({ searchPlaceholder = 'جستجو...', filters = [] }: FilterBarProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [term, setTerm] = useState(searchParams.get('q') ?? '');

  function apply(changes: Record<string, string | null>) {
    const params = new URLSearchParams(searchParams.toString());
    for (const [key, value] of Object.entries(changes)) {
      if (value === null || value === '') params.delete(key);
      else params.set(key, value);
    }
    params.delete('page');
    router.push(`?${params.toString()}`, { scroll: false });
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    apply({ q: term.trim() || null });
  }

  // Which of this bar's own params are set. The reset below clears exactly
  // these and the search term, and nothing else in the URL — a list reached
  // with `?page=3&sort=x` keeps both, because neither is a filter this bar owns.
  const activeParams = [...filters.map((group) => group.param), 'q'].filter((param) =>
    searchParams.get(param),
  );

  return (
    // One bordered region rather than a search box and some loose chips: the
    // controls that narrow the list below now look like the one thing they are.
    <div className="gap-md border-outline-variant/70 bg-surface-container-lowest p-md flex flex-col rounded-xl border">
      <form onSubmit={onSubmit} role="search" className="gap-sm flex items-center">
        <div className="border-outline-variant bg-surface-container-lowest px-md relative flex h-11 flex-1 items-center rounded-lg border">
          <Icon name="search" size={20} className="text-outline shrink-0" />
          <input
            type="search"
            value={term}
            onChange={(event) => setTerm(event.target.value)}
            placeholder={searchPlaceholder}
            aria-label={searchPlaceholder}
            className="px-sm text-body-md text-on-surface placeholder:text-outline w-full border-none bg-transparent outline-none focus:ring-0"
          />
          {term && (
            <button
              type="button"
              aria-label="پاک کردن جستجو"
              onClick={() => {
                setTerm('');
                apply({ q: null });
              }}
              className="text-outline hover:text-primary shrink-0 transition-colors"
            >
              <Icon name="close" size={18} />
            </button>
          )}
        </div>
      </form>

      {filters.map((group) => {
        const active = searchParams.get(group.param);
        return (
          <div key={group.param} className="gap-sm flex flex-wrap items-center">
            <span className="text-caption text-on-surface-variant">{group.label}:</span>

            <button
              type="button"
              onClick={() => apply({ [group.param]: null })}
              className={cn(
                'px-md py-xs text-caption rounded-full font-medium transition-colors',
                !active
                  ? 'bg-primary-fixed text-on-primary-fixed'
                  : 'border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant border',
              )}
            >
              همه
            </button>

            {group.options.map((option) => (
              <button
                key={option.value}
                type="button"
                onClick={() => apply({ [group.param]: option.value })}
                className={cn(
                  'px-md py-xs text-caption rounded-full font-medium transition-colors',
                  active === option.value
                    ? 'bg-primary-fixed text-on-primary-fixed'
                    : 'border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant border',
                )}
              >
                {option.label}
              </button>
            ))}
          </div>
        );
      })}

      {activeParams.length > 0 && (
        <div className="border-outline-variant/60 pt-sm flex items-center border-t">
          <button
            type="button"
            onClick={() => {
              setTerm('');
              apply(Object.fromEntries(activeParams.map((param) => [param, null])));
            }}
            className="gap-xs text-caption text-on-surface-variant hover:text-primary flex items-center font-medium transition-colors"
          >
            <Icon name="filter_alt_off" size={16} />
            پاک کردن فیلترها
          </button>
        </div>
      )}
    </div>
  );
}
