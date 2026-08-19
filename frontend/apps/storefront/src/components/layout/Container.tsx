import { cn } from '@bojan/ui';
import type { ReactNode } from 'react';

/**
 * The layout rail every screen in the design shares:
 * `max-w-[1200px]` centred, 20px gutters on mobile and 64px on desktop.
 *
 * `wide` swaps that cap for the shelf rail — see `maxWidth.shelf` in the
 * Tailwind preset. It is one step out, for a surface whose contents are
 * shelves rather than sentences: on the reading rail the home page's product
 * rows fit four cards and part of a fifth, which looks broken rather than
 * full. Because both caps sit above 1200, the flag changes nothing at any
 * width a phone or tablet reports.
 */
export function Container({
  wide = false,
  className,
  children,
}: {
  wide?: boolean;
  className?: string;
  children: ReactNode;
}) {
  return (
    <div
      className={cn(
        'mx-auto w-full px-margin-mobile md:px-margin-tablet lg:px-margin-desktop',
        wide ? 'max-w-shelf' : 'max-w-content',
        className,
      )}
    >
      {children}
    </div>
  );
}
