'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { Icon, cn } from '@bojan/ui';
import type { MenuCategory } from '@/app/api/categories/route';
import { routes } from '@/lib/routes';

/**
 * Delays around the hover, in milliseconds.
 *
 * Opening waits a moment so a pointer crossing the nav on its way to the cart
 * does not drop a panel over the page on the way past. Closing waits longer
 * than opening because the shopper has to cross the gap between the nav link
 * and the card below it, and a menu that vanishes mid-journey cannot be used
 * with a mouse at all.
 */
const OPEN_DELAY = 120;
const CLOSE_DELAY = 220;

/**
 * How many subsections fill one column before the next one starts.
 *
 * The column count is computed rather than fixed so the card is as wide as its
 * contents need and no wider — a category with four subsections gets one
 * column, one with twenty gets three. Capped at three: past that the card
 * reaches the far edge of the window on the narrowest desktop.
 */
const PER_COLUMN = 8;
const MAX_COLUMNS = 3;
/** Matches the `w-[190px]` on the rows themselves. */
const COLUMN_WIDTH = 190;

/**
 * The header's category menu: a nav link that opens the tree on hover.
 *
 * Shaped after the menu the shopper already knows from the large Iranian
 * stores. A card hangs from the nav link — anchored to it, not spread across
 * the whole bar. The start edge of the card is a rail of the top-level
 * categories, scrolling on its own when the catalogue has more of them than
 * fit; the rest belongs to whichever category the pointer is on, and shows
 * that category's subsections filled column by column, so the card grows a
 * column at a time instead of growing taller. Drawing every category's
 * children at once is the version this replaced: a hundred links dropped over
 * the page, nothing to read, only something to escape from.
 *
 * Every row is a link, both halves. Hovering previews a category, clicking goes
 * to it — the panel never traps a shopper who already knows where they are
 * going.
 *
 * Counts are deliberately absent. «۹ محصول» beside every row is a number nobody
 * navigates by, and thirty of them turn a list that should be scanned by name
 * into a table. The category pages carry the counts.
 *
 * Desktop only, and by construction rather than by a media query — it is
 * rendered inside the `lg:flex` nav, which is the same breakpoint at which the
 * bar stops being the mobile one. Touch users reach the tree through the link,
 * which goes to the categories page exactly as it did before this panel
 * existed; the panel is an accelerator over that page, never the only way in.
 *
 * The tree is fetched once, on the first hover or focus, and kept for the rest
 * of the session. Fetching on mount would cost every page load a request for a
 * panel most visits never open, and fetching per hover would re-request a list
 * that only changes when the catalogue is re-organised.
 */
export function CategoryMenu({ label }: { label: string }) {
  const [open, setOpen] = useState(false);
  // Split from `open` so the card can mount at its start position and
  // transition to its resting one on the next frame.
  const [entered, setEntered] = useState(false);
  const [categories, setCategories] = useState<MenuCategory[] | null>(null);
  /** Whose subsections the wide half is showing. */
  const [active, setActive] = useState<string | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const requested = useRef(false);
  const panelId = useId();
  const pathname = usePathname();

  const clearTimer = useCallback(() => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
  }, []);

  /** Asks for the tree at most once per session. */
  const load = useCallback(() => {
    if (requested.current) return;
    requested.current = true;

    fetch('/api/categories')
      .then((response) => (response.ok ? response.json() : []))
      .then((result: MenuCategory[]) => setCategories(result))
      .catch(() => {
        // The link underneath still reaches the categories page. Retrying on
        // the next hover would be a request per pass of the mouse.
        setCategories([]);
      });
  }, []);

  const schedule = useCallback(
    (next: boolean) => {
      clearTimer();
      if (next) load();
      timer.current = setTimeout(() => setOpen(next), next ? OPEN_DELAY : CLOSE_DELAY);
    },
    [clearTimer, load],
  );

  /** Focus and Escape act at once — there is no pointer in flight to forgive. */
  const closeNow = useCallback(() => {
    clearTimer();
    setOpen(false);
  }, [clearTimer]);

  useEffect(() => clearTimer, [clearTimer]);

  // Otherwise the card is left standing over the page it just navigated to.
  useEffect(closeNow, [pathname, closeNow]);

  useEffect(() => {
    if (!open) {
      setEntered(false);
      // Reopening starts at the top of the tree rather than wherever the
      // pointer happened to leave it a page ago.
      setActive(null);
      return;
    }

    const frame = requestAnimationFrame(() => setEntered(true));
    return () => cancelAnimationFrame(frame);
  }, [open]);

  /*
   * Whichever category the pointer is on, and before it lands on one, the
   * first that actually has children — a card that opens on «این دسته
   * زیرشاخه‌ای ندارد» has spent its wide half saying nothing.
   */
  const current =
    categories?.find((category) => category.slug === active) ??
    categories?.find((category) => category.children.length > 0) ??
    categories?.[0];

  const columns = current
    ? Math.min(MAX_COLUMNS, Math.max(1, Math.ceil(current.children.length / PER_COLUMN)))
    : 1;
  const rows = current ? Math.ceil(current.children.length / columns) : 0;

  return (
    // The anchor for the card: it hangs from the nav link, at the link's start
    // edge, and grows towards the middle of the bar as columns are added.
    <div
      className="relative flex items-center self-stretch"
      onPointerEnter={(event) => {
        // Touch reports a pointer entering and never leaving, which would pin
        // the card open over the page the tap is navigating to.
        if (event.pointerType === 'touch') return;
        schedule(true);
      }}
      onPointerLeave={(event) => {
        if (event.pointerType === 'touch') return;
        schedule(false);
      }}
      onFocus={() => {
        load();
        clearTimer();
        setOpen(true);
      }}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) closeNow();
      }}
      onKeyDown={(event) => {
        if (event.key === 'Escape') closeNow();
      }}
    >
      <Link
        href={routes.categories}
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        className={cn(
          'flex items-center gap-xs text-label-md font-medium transition-colors duration-300 hover:text-secondary-container',
          open ? 'text-secondary-container' : 'text-on-surface-variant',
        )}
      >
        {label}
        <Icon
          name="expand_more"
          size={18}
          aria-hidden="true"
          className={cn(
            'transition-transform duration-300 motion-reduce:transition-none',
            open && '-rotate-180',
          )}
        />
      </Link>

      {open && (
        <div
          id={panelId}
          className={cn(
            // Flush under the bar rather than floating below it: a gap
            // between the two is a strip the pointer can fall through on its
            // way down to the card.
            'paper-card absolute top-full max-w-[calc(100vw-4rem)] overflow-hidden rounded-b-xl shadow-soft',
            // Pinned by its start edge so it opens away from the window edge
            // rather than towards it.
            'start-0',
            'transition-[opacity,transform] duration-200 motion-reduce:transition-none',
            entered ? 'translate-y-0 opacity-100' : '-translate-y-1 opacity-0',
          )}
        >
          {categories === null ? (
            <MenuSkeleton />
          ) : !current ? (
            // Whatever went wrong, the categories page is the answer.
            <Link
              href={routes.categories}
              className="block whitespace-nowrap p-lg text-body-md text-on-surface-variant transition-colors hover:text-primary"
            >
              مشاهده همه دسته‌بندی‌ها
            </Link>
          ) : (
            <div className="flex items-stretch">
              {/*
                The rail scrolls rather than wrapping: a catalogue with thirty
                categories should cost the card a scrollbar, not the width of
                the window.
              */}
              <ul className="max-h-[min(60vh,420px)] w-[220px] shrink-0 overflow-y-auto border-e border-paper-border bg-surface-container/40 py-sm">
                {categories.map((category) => {
                  const selected = category.slug === current.slug;

                  return (
                    <li key={category.slug}>
                      <Link
                        href={routes.category(category.slug)}
                        // Pointer *and* focus, so the wide half follows a mouse
                        // and a Tab key the same way.
                        onPointerEnter={(event) => {
                          if (event.pointerType === 'touch') return;
                          setActive(category.slug);
                        }}
                        onFocus={() => setActive(category.slug)}
                        className={cn(
                          'flex items-center gap-sm px-md py-sm transition-colors',
                          selected
                            ? 'bg-warm-paper text-secondary'
                            : 'text-on-surface-variant hover:bg-warm-paper/60',
                        )}
                      >
                        <Icon
                          name={category.icon}
                          size={20}
                          aria-hidden="true"
                          className="shrink-0"
                        />
                        <span className="flex-1 truncate text-label-md font-label-md">
                          {category.name}
                        </span>
                        <Icon
                          name="chevron_left"
                          size={18}
                          aria-hidden="true"
                          className={cn('shrink-0', !selected && 'opacity-0')}
                        />
                      </Link>
                    </li>
                  );
                })}
              </ul>

              <div className="flex min-h-[220px] flex-col gap-md p-lg">
                <Link
                  href={routes.category(current.slug)}
                  className="inline-flex w-fit items-center gap-xs whitespace-nowrap text-label-md font-label-md text-primary-container transition-colors hover:text-secondary"
                >
                  <span aria-hidden="true" className="h-4 w-1 rounded-full bg-secondary" />
                  همه محصولات {current.name}
                  <Icon name="chevron_left" size={18} aria-hidden="true" />
                </Link>

                {current.children.length > 0 ? (
                  /*
                    Filled down a column and then across, the way a list is
                    read, rather than across and then down — `grid-flow-col`
                    with the row count worked out above.
                  */
                  <ul
                    className="grid grid-flow-col gap-x-lg"
                    style={{
                      gridTemplateColumns: `repeat(${columns}, ${COLUMN_WIDTH}px)`,
                      gridTemplateRows: `repeat(${rows}, auto)`,
                    }}
                  >
                    {current.children.map((child) => (
                      <li key={child.slug}>
                        <Link
                          href={`${routes.category(current.slug)}?sub=${child.slug}`}
                          className="block truncate py-xs text-body-md text-on-surface-variant transition-colors hover:text-secondary"
                        >
                          {child.name}
                        </Link>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="w-[190px] text-caption text-on-surface-variant">
                    این دسته زیرشاخه‌ای ندارد.
                  </p>
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

/** The card's two halves in grey, so it opens at its real size on a cold cache. */
function MenuSkeleton() {
  return (
    <div aria-hidden="true" className="flex min-h-[220px] items-stretch">
      <div className="w-[220px] shrink-0 border-e border-paper-border bg-surface-container/40 py-sm">
        {[0, 1, 2, 3, 4, 5].map((row) => (
          <div key={row} className="flex items-center gap-sm px-md py-sm">
            <span className="h-5 w-5 shrink-0 animate-pulse rounded-full bg-surface-variant" />
            <span className="h-4 w-28 animate-pulse rounded-full bg-surface-variant" />
          </div>
        ))}
      </div>

      <div className="flex w-[190px] flex-col gap-md p-lg">
        <span className="h-4 w-32 animate-pulse rounded-full bg-surface-variant" />
        <div className="flex flex-col gap-sm">
          {[0, 1, 2, 3].map((row) => (
            <span key={row} className="h-3 w-24 animate-pulse rounded-full bg-surface-variant" />
          ))}
        </div>
      </div>
    </div>
  );
}
