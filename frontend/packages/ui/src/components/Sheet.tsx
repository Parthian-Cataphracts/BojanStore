'use client';

import { useEffect, useRef, type ReactNode } from 'react';
import { cn } from '../lib/cn';
import { IconButton } from './IconButton';

export interface SheetProps {
  open: boolean;
  onClose: () => void;
  title?: string;
  /** `bottom` matches the mobile filter/sort sheets; `side` is the desktop drawer. */
  placement?: 'bottom' | 'side';
  children: ReactNode;
  footer?: ReactNode;
  className?: string;
}

/** Everything focusable, in document order, ignoring anything disabled. */
const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Bottom sheet / side drawer. Used by the mobile filter, sort and cart
 * overlays in the design, and by the panel's navigation drawer.
 *
 * It is a modal, so it behaves like one: focus moves inside on open, Tab is
 * trapped within it, Escape closes it, the page behind cannot scroll, and focus
 * returns to whatever opened it. Without the trap a keyboard user tabs straight
 * out of the dialog into a page they cannot see.
 */
export function Sheet({
  open,
  onClose,
  title,
  placement = 'bottom',
  children,
  footer,
  className,
}: SheetProps) {
  const panelRef = useRef<HTMLDivElement>(null);
  const previouslyFocused = useRef<HTMLElement | null>(null);

  /**
   * Held in a ref so it is not a dependency of the effect below.
   *
   * Every caller passes an inline arrow, which is a new function on every
   * render — so with `onClose` in the dependency list the whole effect tore down
   * and set up again after *any* state change inside the sheet, and setting up
   * means "move focus to the first control". In the chat that is one keystroke:
   * type a character, the draft state changes, focus jumps to the close button
   * and the caret vanishes. Nothing about closing changes while the sheet is
   * open, so only `open` belongs in the list.
   */
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  useEffect(() => {
    if (!open) return;

    // Remember where focus was so it can go back when the sheet closes.
    previouslyFocused.current = document.activeElement as HTMLElement | null;

    const panel = panelRef.current;
    // Prefer the first control; fall back to the panel so focus is never left
    // behind on the page underneath.
    const first = panel?.querySelector<HTMLElement>(FOCUSABLE);
    (first ?? panel)?.focus();

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onCloseRef.current();
        return;
      }

      if (event.key !== 'Tab' || !panel) return;

      const focusable = [...panel.querySelectorAll<HTMLElement>(FOCUSABLE)].filter(
        (element) => element.offsetParent !== null,
      );
      if (focusable.length === 0) {
        event.preventDefault();
        return;
      }

      const firstElement = focusable[0]!;
      const lastElement = focusable[focusable.length - 1]!;
      const active = document.activeElement;

      // Wrap around at both ends rather than letting focus escape the dialog.
      if (event.shiftKey && (active === firstElement || active === panel)) {
        event.preventDefault();
        lastElement.focus();
      } else if (!event.shiftKey && active === lastElement) {
        event.preventDefault();
        firstElement.focus();
      }
    };

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    document.addEventListener('keydown', onKeyDown);

    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener('keydown', onKeyDown);
      // The opener may have unmounted while the sheet was up.
      if (previouslyFocused.current?.isConnected) previouslyFocused.current.focus();
    };
  }, [open]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex" role="dialog" aria-modal="true" aria-label={title}>
      <button
        type="button"
        aria-label="بستن"
        onClick={onClose}
        className="absolute inset-0 bg-inverse-surface/40 backdrop-blur-[2px]"
      />

      <div
        ref={panelRef}
        tabIndex={-1}
        className={cn(
          'relative z-10 flex flex-col bg-surface-container-lowest shadow-soft outline-none',
          placement === 'bottom'
            ? 'mt-auto max-h-[85vh] w-full rounded-t-32'
            : 'ms-auto h-full w-full max-w-md',
          className,
        )}
      >
        {placement === 'bottom' && (
          <span className="mx-auto mt-sm h-1 w-10 rounded-full bg-outline-variant" />
        )}

        {title && (
          <header className="flex items-center justify-between border-b border-outline-variant/40 px-lg py-md">
            <h2 className="font-headline text-display-md text-primary">{title}</h2>
            <IconButton icon="close" label="بستن" onClick={onClose} />
          </header>
        )}

        <div className="flex-1 overflow-y-auto p-lg">{children}</div>

        {footer && (
          <footer className="border-t border-outline-variant/40 p-lg pb-safe">{footer}</footer>
        )}
      </div>
    </div>
  );
}
