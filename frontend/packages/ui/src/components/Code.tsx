import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '../lib/cn';

export interface CodeProps extends HTMLAttributes<HTMLSpanElement> {
  /** Render as a block that owns its alignment rather than flowing inline. */
  block?: boolean;
  children: ReactNode;
}

/**
 * Latin technical value — order code, SKU, API key, webhook URL, e-mail.
 *
 * Switches to the Latin face and isolates the bidi run so a Latin string
 * embedded in Persian copy keeps its own character order. Persian UI text must
 * never use this.
 */
export function Code({ block = false, className, children, ...props }: CodeProps) {
  return (
    <span className={cn(block ? 'latin' : 'latin-inline', className)} {...props}>
      {children}
    </span>
  );
}
