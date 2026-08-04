import { cn } from '@bojan/ui';
import type { ReactNode } from 'react';

/**
 * The layout rail every screen in the design shares:
 * `max-w-[1200px]` centred, 20px gutters on mobile and 64px on desktop.
 */
export function Container({
  className,
  children,
}: {
  className?: string;
  children: ReactNode;
}) {
  return (
    <div className={cn('mx-auto w-full max-w-content px-margin-mobile md:px-margin-tablet lg:px-margin-desktop', className)}>
      {children}
    </div>
  );
}
