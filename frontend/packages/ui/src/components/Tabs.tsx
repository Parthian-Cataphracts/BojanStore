'use client';

import { useRef } from 'react';
import { cn } from '../lib/cn';
import { toPersianDigits } from '../lib/format';

export interface TabItem {
  id: string;
  label: string;
  /** Rendered beside the label as a count pill, e.g. how many reviews a tab holds. */
  count?: number;
}

export interface TabsProps {
  items: TabItem[];
  value: string;
  onChange: (id: string) => void;
  /** `pill` matches the horizontal chip rows; `underline` the product tabs. */
  variant?: 'pill' | 'underline';
  /**
   * Prefix for the `id`/`aria-controls` pair.
   *
   * Without it a tab is a button that changes something unnamed: a screen
   * reader announces the tab and then has no way to reach the panel it opened.
   * The panel is expected to carry `id={`${idBase}-panel-${tabId}`}`.
   */
  idBase?: string;
  className?: string;
}

/**
 * A tab strip.
 *
 * Arrow keys move between tabs rather than Tab doing it, which is what the
 * pattern requires and also what makes a five-tab strip bearable: Tab moves out
 * of the strip and into the panel, so reaching the content behind a tab is one
 * key rather than five. `Home`/`End` jump to either end.
 *
 * Right and left are swapped against their names on purpose — the strip is
 * rendered RTL, so «next» is the arrow pointing left.
 */
export function Tabs({
  items,
  value,
  onChange,
  variant = 'pill',
  idBase,
  className,
}: TabsProps) {
  const strip = useRef<HTMLDivElement>(null);

  function focusTab(index: number) {
    const wrapped = (index + items.length) % items.length;
    const next = items[wrapped];
    if (!next) return;

    onChange(next.id);
    // The button has to exist before it can take focus, and it does — every
    // tab is rendered whichever one is selected.
    strip.current?.querySelectorAll<HTMLButtonElement>('[role="tab"]')[wrapped]?.focus();
  }

  function onKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    const current = items.findIndex((item) => item.id === value);
    if (current < 0) return;

    switch (event.key) {
      case 'ArrowLeft':
        focusTab(current + 1);
        break;
      case 'ArrowRight':
        focusTab(current - 1);
        break;
      case 'Home':
        focusTab(0);
        break;
      case 'End':
        focusTab(items.length - 1);
        break;
      default:
        return;
    }

    event.preventDefault();
  }

  return (
    <div
      ref={strip}
      role="tablist"
      onKeyDown={onKeyDown}
      className={cn(
        'hide-scrollbar gap-sm flex overflow-x-auto',
        variant === 'underline' && 'border-outline-variant/40 border-b',
        className,
      )}
    >
      {items.map((item) => {
        const active = item.id === value;
        return (
          <button
            key={item.id}
            role="tab"
            type="button"
            aria-selected={active}
            {...(idBase
              ? { id: `${idBase}-tab-${item.id}`, 'aria-controls': `${idBase}-panel-${item.id}` }
              : null)}
            // Only the selected tab is in the tab order; the arrows reach the
            // rest. See the note above.
            tabIndex={active ? 0 : -1}
            onClick={() => onChange(item.id)}
            className={cn(
              'gap-xs text-label-md font-label-md flex shrink-0 items-center whitespace-nowrap transition-all',
              variant === 'pill'
                ? cn(
                    'px-lg py-sm rounded-full',
                    active
                      ? 'bg-primary-container text-on-primary'
                      : 'text-on-surface-variant hover:bg-surface-container-low hover:text-primary',
                  )
                : cn(
                    'px-md pb-sm pt-md border-b-2',
                    active
                      ? 'border-secondary-container text-primary'
                      : 'border-transparent text-on-surface-variant hover:text-primary',
                  ),
            )}
          >
            {item.label}
            {item.count !== undefined && item.count > 0 && (
              <span
                className={cn(
                  'tabular text-helper rounded-full px-2 py-0.5 leading-none',
                  active
                    ? 'bg-secondary-container/25 text-primary'
                    : 'bg-surface-container-high text-on-surface-variant',
                )}
              >
                {toPersianDigits(item.count)}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
