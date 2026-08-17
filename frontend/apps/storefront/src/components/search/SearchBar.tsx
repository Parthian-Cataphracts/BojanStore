'use client';

import Image from 'next/image';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Icon, formatPrice, toPersianDigits } from '@bojan/ui';
import { routes } from '@/lib/routes';
import type { SearchSuggestions } from '@/app/api/search-suggestions/route';

/**
 * The search field from screen 05, with the first few matches under it.
 *
 * The box suggests rather than searches: five products, and a last row that
 * opens the full results. That split is the point — five is what fits under a
 * field without becoming a page, and the row underneath says how many there
 * really are, so somebody looking at «آبرنگ» knows whether they are seeing five
 * of five or five of forty before deciding to press it.
 *
 * Everything the box offers, the results page can find: it calls the same
 * product search, so a suggestion is never something the page it leads to
 * cannot show. Submitting the form skips the suggestions entirely and goes
 * straight there, which is what somebody who typed the whole word wants.
 */

/** Long enough that a fast typist makes one request per word, not per letter. */
const DEBOUNCE_MS = 250;

export function SearchBar({ autoFocus = false }: { autoFocus?: boolean }) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [term, setTerm] = useState(searchParams.get('q') ?? '');
  const [suggestions, setSuggestions] = useState<SearchSuggestions | null>(null);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const trimmed = term.trim();

  // Fetch on a delay, and let a newer keystroke cancel an older answer: without
  // the guard the reply to «آب» can land after the reply to «آبرنگ» and put the
  // wider list back under the narrower word.
  useEffect(() => {
    if (trimmed.length < 2) {
      setSuggestions(null);
      setLoading(false);
      return;
    }

    let cancelled = false;
    setLoading(true);

    const timer = window.setTimeout(() => {
      fetch(`/api/search-suggestions?q=${encodeURIComponent(trimmed)}`, { cache: 'no-store' })
        .then((response) => (response.ok ? response.json() : null))
        .then((data: SearchSuggestions | null) => {
          if (!cancelled) setSuggestions(data);
        })
        .catch(() => {
          // The form still submits; the results page is the real answer.
          if (!cancelled) setSuggestions(null);
        })
        .finally(() => {
          if (!cancelled) setLoading(false);
        });
    }, DEBOUNCE_MS);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [trimmed]);

  // Pointer-down rather than blur: blur fires before a click on a suggestion
  // registers, so closing on it would close the list out from under the finger
  // that was choosing from it.
  useEffect(() => {
    if (!open) return;

    const onPointerDown = (event: MouseEvent | TouchEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };

    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('touchstart', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('touchstart', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  function goToResults() {
    setOpen(false);
    router.push(trimmed ? `${routes.search}?q=${encodeURIComponent(trimmed)}` : routes.search);
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    goToResults();
  }

  const items = suggestions?.items ?? [];
  const total = suggestions?.total ?? 0;
  const showPanel = open && trimmed.length >= 2;
  const listId = 'search-suggestions';

  return (
    <div ref={containerRef} className="relative">
      <form onSubmit={onSubmit} role="search" className="gap-sm flex items-center">
        <div className="border-surface-variant bg-soft-mint/30 px-md relative flex h-12 flex-1 items-center rounded-lg border-b">
          <Icon name="search" className="text-primary" />
          <input
            // Not type="search". That draws the browser's own clear button, and
            // this field already has one below — so the box showed two crosses,
            // one styled to match the site and one not, side by side.
            type="text"
            value={term}
            autoFocus={autoFocus}
            onChange={(event) => {
              setTerm(event.target.value);
              setOpen(true);
            }}
            onFocus={() => setOpen(true)}
            placeholder="دنبال چی می‌گردی؟"
            aria-label="جستجوی محصولات"
            role="combobox"
            aria-expanded={showPanel}
            aria-controls={listId}
            aria-autocomplete="list"
            autoComplete="off"
            className="px-sm text-body-md text-on-surface placeholder:text-outline-variant w-full border-none bg-transparent outline-none focus:ring-0"
          />
          {term && (
            <button
              type="button"
              aria-label="پاک کردن جستجو"
              onClick={() => {
                setTerm('');
                setOpen(false);
              }}
              className="text-outline hover:text-primary transition-colors"
            >
              <Icon name="close" size={20} />
            </button>
          )}
        </div>
      </form>

      {showPanel && (
        <div
          id={listId}
          role="listbox"
          className="mt-xs border-outline-variant bg-surface-container-lowest shadow-soft absolute inset-x-0 top-full z-40 overflow-hidden rounded-xl border"
        >
          {items.length === 0 ? (
            <p className="px-lg py-lg text-caption text-on-surface-variant text-center">
              {loading ? 'در حال جستجو…' : 'محصولی با این نام پیدا نشد.'}
            </p>
          ) : (
            <>
              <ul className="max-h-[60vh] overflow-y-auto">
                {items.map((product) => (
                  <li key={product.slug} role="option" aria-selected="false">
                    <Link
                      href={routes.product(product.slug)}
                      onClick={() => setOpen(false)}
                      className="gap-md px-md py-sm hover:bg-surface-container-low flex items-center transition-colors"
                    >
                      <Image
                        src={product.image}
                        alt={product.imageAlt}
                        width={48}
                        height={48}
                        className="h-12 w-12 shrink-0 rounded-lg object-cover"
                      />
                      <span className="gap-xs flex min-w-0 flex-1 flex-col">
                        <span className="text-body-md text-on-surface truncate">
                          {product.title}
                        </span>
                        <span className="text-caption text-on-surface-variant truncate">
                          {product.brand}
                        </span>
                      </span>
                      <span className="text-caption font-label-md text-primary shrink-0">
                        {formatPrice(product.price)}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>

              {/*
                The way out of the box and into the results.

                It counts, because «نمایش بیشتر» over a list of five does not
                say whether there is a sixth. A shopper deciding whether to
                press it is asking exactly that.
              */}
              <button
                type="button"
                onClick={goToResults}
                className="gap-sm border-outline-variant/60 px-md py-md text-label-md text-primary hover:bg-soft-mint/40 flex w-full items-center justify-between border-t transition-colors"
              >
                <span className="gap-xs flex items-center">
                  <Icon name="search" size={18} />
                  {total > items.length
                    ? `نمایش همه‌ی ${toPersianDigits(total)} نتیجه`
                    : 'مشاهده در صفحه‌ی نتایج'}
                </span>
                <Icon name="chevron_left" size={18} />
              </button>
            </>
          )}
        </div>
      )}
    </div>
  );
}
