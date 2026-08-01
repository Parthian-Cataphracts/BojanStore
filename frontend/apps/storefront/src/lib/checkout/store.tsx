'use client';

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';

/**
 * What the guided checkout (screens 71-80) has collected so far.
 *
 * The guided flow is a sequence of pages, and a choice made on one has to
 * survive the navigation to the next. Every screen used to hold its selection
 * in a component's own `useState`, so all of it — the address, the shipping
 * method, the delivery window, the payment method — was discarded on the way to
 * the following step, and the "تایید نهایی" screen linked straight to a success
 * page without an order existing at all.
 *
 * `sessionStorage` rather than `localStorage`: this is one shopping session's
 * work in progress, not a basket to come back to next week, and leaving a stale
 * address selected days later is worse than starting the flow again. The basket
 * itself stays in the cart store, which is the thing that does persist.
 */

const STORAGE_KEY = 'bojan.checkout.v1';

export interface CheckoutSelection {
  addressId?: string;
  shippingMethodId?: string;
  paymentMethodId?: string;
  /** Screen 74's day and slot, already formatted for display and for the order. */
  deliveryWindow?: string;
  note?: string;
}

interface CheckoutContextValue {
  selection: CheckoutSelection;
  /** False until `sessionStorage` has been read — see the note in the provider. */
  hydrated: boolean;
  select: (patch: CheckoutSelection) => void;
  reset: () => void;
}

const CheckoutContext = createContext<CheckoutContextValue | null>(null);

function readStorage(): CheckoutSelection {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return {};

    const parsed: unknown = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return {};

    // Read field by field rather than cast: this comes from storage the page
    // does not control, and a wrong shape here would reach the order body.
    const value = parsed as Record<string, unknown>;
    const pick = (key: string) => (typeof value[key] === 'string' ? (value[key] as string) : undefined);

    return {
      ...(pick('addressId') ? { addressId: pick('addressId')! } : null),
      ...(pick('shippingMethodId') ? { shippingMethodId: pick('shippingMethodId')! } : null),
      ...(pick('paymentMethodId') ? { paymentMethodId: pick('paymentMethodId')! } : null),
      ...(pick('deliveryWindow') ? { deliveryWindow: pick('deliveryWindow')! } : null),
      ...(pick('note') ? { note: pick('note')! } : null),
    };
  } catch {
    // Blocked storage or malformed JSON — the flow starts empty rather than
    // failing, and every step re-asks for what it needs.
    return {};
  }
}

export function CheckoutProvider({ children }: { children: ReactNode }) {
  const [selection, setSelection] = useState<CheckoutSelection>({});
  const [hydrated, setHydrated] = useState(false);

  // Read after mount, never during render: the server has no sessionStorage,
  // and reading inline would make the first client render disagree with the
  // server HTML.
  useEffect(() => {
    setSelection(readStorage());
    setHydrated(true);
  }, []);

  useEffect(() => {
    if (!hydrated) return;
    try {
      window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(selection));
    } catch {
      // Storage blocked — the selection still works for this page view.
    }
  }, [selection, hydrated]);

  const select = useCallback((patch: CheckoutSelection) => {
    setSelection((current) => ({ ...current, ...patch }));
  }, []);

  const reset = useCallback(() => {
    setSelection({});
    try {
      window.sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Nothing to clean up if storage was never available.
    }
  }, []);

  const value = useMemo<CheckoutContextValue>(
    () => ({ selection, hydrated, select, reset }),
    [selection, hydrated, select, reset],
  );

  return <CheckoutContext.Provider value={value}>{children}</CheckoutContext.Provider>;
}

export function useCheckout(): CheckoutContextValue {
  const context = useContext(CheckoutContext);
  if (!context) {
    throw new Error('useCheckout must be used inside a CheckoutProvider.');
  }
  return context;
}
