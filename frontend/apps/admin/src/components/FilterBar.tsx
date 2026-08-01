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

  return (
    <div className="flex flex-col gap-md">
      <form onSubmit={onSubmit} role="search" className="flex items-center gap-sm">
        <div className="relative flex h-11 flex-1 items-center rounded-lg border border-outline-variant bg-surface-container-lowest px-md">
          <Icon name="search" size={20} className="shrink-0 text-outline" />
          <input
            type="search"
            value={term}
            onChange={(event) => setTerm(event.target.value)}
            placeholder={searchPlaceholder}
            aria-label={searchPlaceholder}
            className="w-full border-none bg-transparent px-sm text-body-md text-on-surface outline-none placeholder:text-outline focus:ring-0"
          />
          {term && (
            <button
              type="button"
              aria-label="پاک کردن جستجو"
              onClick={() => {
                setTerm('');
                apply({ q: null });
              }}
              className="shrink-0 text-outline transition-colors hover:text-primary"
            >
              <Icon name="close" size={18} />
            </button>
          )}
        </div>
      </form>

      {filters.map((group) => {
        const active = searchParams.get(group.param);
        return (
          <div key={group.param} className="flex flex-wrap items-center gap-sm">
            <span className="text-caption text-on-surface-variant">{group.label}:</span>

            <button
              type="button"
              onClick={() => apply({ [group.param]: null })}
              className={cn(
                'rounded-full px-md py-xs text-caption font-medium transition-colors',
                !active
                  ? 'bg-primary-fixed text-on-primary-fixed'
                  : 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant',
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
                  'rounded-full px-md py-xs text-caption font-medium transition-colors',
                  active === option.value
                    ? 'bg-primary-fixed text-on-primary-fixed'
                    : 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant',
                )}
              >
                {option.label}
              </button>
            ))}
          </div>
        );
      })}
    </div>
  );
}
