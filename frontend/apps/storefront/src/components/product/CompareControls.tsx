'use client';

import Image from 'next/image';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Icon, Sheet } from '@bojan/ui';
import { routes } from '@/lib/routes';

export interface CompareCandidate {
  slug: string;
  title: string;
  image: string;
  brand: string;
}

/** Screen 60 draws four column slots. */
const MAX_COLUMNS = 4;

function hrefFor(slugs: string[]): string {
  return slugs.length > 0
    ? `${routes.compare}?slugs=${encodeURIComponent(slugs.join(','))}`
    : routes.compare;
}

/**
 * Add and remove columns on screen 60.
 *
 * The comparison lives in the URL rather than in storage, like the catalogue's
 * filters: a comparison is worth sending to someone, and the back button should
 * undo a column you did not mean to add.
 *
 * The design draws an `add` slot and a `close` on each column but gives no
 * screen to pick from, so the picker is a sheet — the same one the rest of the
 * storefront uses for its overlays.
 */
export function CompareControls({
  slugs,
  candidates,
  slot,
  removeLabel,
}: {
  slugs: string[];
  candidates: CompareCandidate[];
  /** `remove` renders one column's close button; `add` the empty slot. */
  slot: 'remove' | 'add';
  /** Which column `remove` belongs to. */
  removeLabel?: { slug: string; title: string };
}) {
  const router = useRouter();
  const [picking, setPicking] = useState(false);

  if (slot === 'remove') {
    if (!removeLabel) return null;

    return (
      <button
        type="button"
        aria-label={`حذف ${removeLabel.title} از مقایسه`}
        onClick={() => router.push(hrefFor(slugs.filter((item) => item !== removeLabel.slug)))}
        className="absolute end-1 top-1 z-10 flex h-7 w-7 items-center justify-center rounded-full bg-surface-container-lowest/80 text-outline backdrop-blur-sm transition-colors hover:bg-error-container hover:text-error"
      >
        <Icon name="close" size={18} />
      </button>
    );
  }

  const full = slugs.length >= MAX_COLUMNS;
  const available = candidates.filter((item) => !slugs.includes(item.slug));

  return (
    <>
      <button
        type="button"
        disabled={full}
        onClick={() => setPicking(true)}
        aria-label="افزودن محصول به مقایسه"
        className="flex w-40 flex-col items-center justify-center gap-sm rounded-lg border border-dashed border-outline-variant p-lg text-on-surface-variant transition-colors hover:border-primary hover:text-primary disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:border-outline-variant disabled:hover:text-on-surface-variant"
      >
        <Icon name="add" size={28} />
        <span className="text-caption">{full ? 'حداکثر چهار محصول' : 'افزودن محصول'}</span>
      </button>

      <Sheet open={picking} onClose={() => setPicking(false)} title="افزودن به مقایسه">
        {available.length === 0 ? (
          <p className="text-body-md text-on-surface-variant">
            محصول دیگری برای افزودن باقی نمانده است.
          </p>
        ) : (
          <ul className="flex flex-col gap-sm">
            {available.map((item) => (
              <li key={item.slug}>
                <button
                  type="button"
                  onClick={() => {
                    setPicking(false);
                    router.push(hrefFor([...slugs, item.slug]));
                  }}
                  className="flex w-full items-center gap-md rounded-lg p-sm text-start transition-colors hover:bg-surface-container-low"
                >
                  <span className="relative h-12 w-12 shrink-0 overflow-hidden rounded-lg bg-surface-container-highest">
                    <Image src={item.image} alt="" fill sizes="48px" className="object-cover" />
                  </span>

                  <span className="flex min-w-0 flex-col">
                    <span className="line-clamp-1 text-body-md text-on-surface">{item.title}</span>
                    <span className="text-caption text-outline">{item.brand}</span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </Sheet>
    </>
  );
}

export { MAX_COLUMNS };
