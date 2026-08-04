'use client';

import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  type ReactNode,
} from 'react';

/**
 * The choices the guided checkout collects across its steps.
 *
 * The seven screens (71-80) each ask one question — address, shipping tier,
 * delivery window, payment method — and the answers have to survive the
 * navigation between them. Without somewhere to keep them, each step's radio
 * group was local state that unmounted the moment the shopper pressed
 * "continue", the review screen showed the first option of each list whatever
 * had been picked, and the final button linked to a success page without ever
 * placing an order.
 *
 * `sessionStorage`, not `localStorage`: a checkout in progress belongs to the
 * tab it was started in and should not still be waiting a week later, unlike
 * the basket itself.
 */

const STORAGE_KEY = 'bojan.checkout.v1';
const STORAGE_VERSION = 1;

export interface CheckoutSelection {
  addressId?: string;
  shippingMethodId?: string;
  /** Day and window ids from the delivery-time step, kept as the shopper's stated preference. */
  deliveryDayId?: string;
  deliverySlotId?: string;
  paymentMethodId?: string;
  note?: string;
}

interface PersistedSelection extends CheckoutSelection {
  v: number;
}

interface SelectionState extends CheckoutSelection {
  /** False until session storage has been read — see the provider. */
  hydrated: boolean;
}

type SelectionAction =
  | { type: 'hydrate'; state: CheckoutSelection }
  | { type: 'set'; patch: CheckoutSelection }
  | { type: 'clear' };

const initialState: SelectionState = { hydrated: false };

function reducer(state: SelectionState, action: SelectionAction): SelectionState {
  switch (action.type) {
    case 'hydrate':
      return { ...action.state, hydrated: true };

    case 'set':
      return { ...state, ...action.patch };

    case 'clear':
      return { hydrated: true };

    default:
      return state;
  }
}

/** Storage is untrusted: anything that is not a string is dropped. */
function text(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 && value.length <= 200 ? value : undefined;
}

function readStorage(): CheckoutSelection | null {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as PersistedSelection;
    if (parsed.v !== STORAGE_VERSION) return null;

    return {
      ...(text(parsed.addressId) ? { addressId: parsed.addressId } : null),
      ...(text(parsed.shippingMethodId) ? { shippingMethodId: parsed.shippingMethodId } : null),
      ...(text(parsed.deliveryDayId) ? { deliveryDayId: parsed.deliveryDayId } : null),
      ...(text(parsed.deliverySlotId) ? { deliverySlotId: parsed.deliverySlotId } : null),
      ...(text(parsed.paymentMethodId) ? { paymentMethodId: parsed.paymentMethodId } : null),
      ...(text(parsed.note) ? { note: parsed.note } : null),
    };
  } catch {
    // Corrupt or unavailable storage (private mode, quota) — start clean.
    return null;
  }
}

export interface CheckoutSelectionValue {
  selection: CheckoutSelection;
  hydrated: boolean;
  select: (patch: CheckoutSelection) => void;
  clear: () => void;
}

const CheckoutSelectionContext = createContext<CheckoutSelectionValue | null>(null);

export function CheckoutSelectionProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(reducer, initialState);

  // After mount, never during render: the server has no `sessionStorage`, and
  // reading it inline would make the first client render disagree with the
  // server HTML.
  useEffect(() => {
    dispatch({ type: 'hydrate', state: readStorage() ?? {} });
  }, []);

  useEffect(() => {
    if (!state.hydrated) return;

    try {
      // `hydrated` is this provider's own bookkeeping, not part of the
      // selection, so it is rebuilt on read rather than written out.
      const { hydrated, ...selection } = state;
      void hydrated;

      window.sessionStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({ v: STORAGE_VERSION, ...selection } satisfies PersistedSelection),
      );
    } catch {
      // Storage full or blocked — the flow still works for this navigation.
    }
  }, [state]);

  const value = useMemo<CheckoutSelectionValue>(() => {
    const { hydrated, ...selection } = state;

    return {
      selection,
      hydrated,
      select: (patch) => dispatch({ type: 'set', patch }),
      clear: () => dispatch({ type: 'clear' }),
    };
  }, [state]);

  return (
    <CheckoutSelectionContext.Provider value={value}>{children}</CheckoutSelectionContext.Provider>
  );
}

export function useCheckoutSelection(): CheckoutSelectionValue {
  const value = useContext(CheckoutSelectionContext);
  if (!value) {
    throw new Error('useCheckoutSelection must be used inside a CheckoutSelectionProvider.');
  }
  return value;
}
