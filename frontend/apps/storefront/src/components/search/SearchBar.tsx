'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Icon } from '@bojan/ui';
import { routes } from '@/lib/routes';

/** The search field from screen 05, wired to the `?q=` param. */
export function SearchBar({ autoFocus = false }: { autoFocus?: boolean }) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [term, setTerm] = useState(searchParams.get('q') ?? '');

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmed = term.trim();
    router.push(trimmed ? `${routes.search}?q=${encodeURIComponent(trimmed)}` : routes.search);
  }

  return (
    <form onSubmit={onSubmit} role="search" className="flex items-center gap-sm">
      <div className="relative flex h-12 flex-1 items-center rounded-lg border-b border-surface-variant bg-soft-mint/30 px-md">
        <Icon name="search" className="text-primary" />
        <input
          type="search"
          value={term}
          autoFocus={autoFocus}
          onChange={(event) => setTerm(event.target.value)}
          placeholder="دنبال چی می‌گردی؟"
          aria-label="جستجوی محصولات"
          className="w-full border-none bg-transparent px-sm text-body-md text-on-surface outline-none placeholder:text-outline-variant focus:ring-0"
        />
        {term && (
          <button
            type="button"
            aria-label="پاک کردن جستجو"
            onClick={() => setTerm('')}
            className="text-outline transition-colors hover:text-primary"
          >
            <Icon name="close" size={20} />
          </button>
        )}
      </div>
    </form>
  );
}
