'use client';

import { useEffect, useRef, type ReactNode } from 'react';
import { cn } from '@bojan/ui';

/**
 * A page's own action bar: stuck above the tab bar on a phone, inline in the
 * page from `md` up.
 *
 * The product page's buy bar and the return form's submit bar were each
 * carrying their own copy of the same twenty classes, including the same
 * `bottom-[72px]` — a number that was wrong in both, since the tab bar it was
 * meant to clear stands 87px tall. Sitting on `--bottom-inset` instead means
 * the bar rests on whatever the tab bar actually measures, at every width and
 * on a notched phone.
 *
 * While it is fixed it publishes its own height, so anything floating above
 * the bottom edge — the chat launcher — clears this bar too rather than
 * landing on top of it. The measurement is taken from the element rather than
 * from a breakpoint constant, so it stays right if the bar wraps to two lines
 * or the CSS decides at some other width that it should be inline.
 */
export function StickyActionBar({
  children,
  className,
  inlineFrom = 'md',
}: {
  children: ReactNode;
  className?: string;
  /**
   * The breakpoint at which the bar stops floating and joins the page.
   *
   * `md` for the pages whose layout is one column until then. The cart keeps
   * its bar floating to `lg`, because that is where its summary column appears
   * beside the list and takes the button back.
   */
  inlineFrom?: 'md' | 'lg';
}) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const element = ref.current;
    if (!element) return;

    function publish() {
      if (!element) return;
      // Inline in the page from `md` up, where it takes its own space in the
      // flow and there is nothing for anyone else to clear.
      const stacked = window.getComputedStyle(element).position === 'fixed';
      document.documentElement.style.setProperty(
        '--bottom-action-height',
        stacked ? `${element.offsetHeight}px` : '0px',
      );
    }

    publish();

    // Catches the bar growing — a wrapped label, a longer button — and the
    // resize listener catches it changing position without changing size.
    const observer = new ResizeObserver(publish);
    observer.observe(element);
    window.addEventListener('resize', publish);

    return () => {
      observer.disconnect();
      window.removeEventListener('resize', publish);
      document.documentElement.style.removeProperty('--bottom-action-height');
    };
  }, []);

  return (
    <div
      ref={ref}
      className={cn(
        'glass-nav above-bottom-nav fixed inset-x-0 z-40 flex flex-wrap gap-md border-t border-outline-variant/40 px-margin-mobile py-md',
        // Written out rather than interpolated: Tailwind reads these class
        // names from the source, and `${prefix}:static` is not a name it finds.
        inlineFrom === 'lg'
          ? 'lg:static lg:border-0 lg:bg-transparent lg:p-0 lg:backdrop-blur-none'
          : 'md:static md:border-0 md:bg-transparent md:p-0 md:backdrop-blur-none',
        className,
      )}
    >
      {children}
    </div>
  );
}
