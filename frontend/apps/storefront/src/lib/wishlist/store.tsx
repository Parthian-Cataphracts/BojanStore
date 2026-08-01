'use client';

import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  type ReactNode,
} from 'react';
import { postJson } from '@/lib/api/submit';
import type { Product } from '@/lib/api/types';

/**
 * The wishlist.
 *
 * Same shape as the cart store, and for the same reason: the heart sits on
 * every catalogue card, so the saved state has to be readable from anywhere and
 * has to survive a reload. It is kept as whole products rather than ids so the
 * wishlist screen can render without a second round-trip.
 *
 * When the customer is signed in the change is mirrored to `/api/account/...`;
 * the local state is the source of truth either way, so a failed call does not
 * strand the interface. Once the backend owns the list, this file is where the
 * two swap over.
 */

const STORAGE_KEY = 'bojan.wishlist.v1';
const STORAGE_VERSION = 1;
/** Enough for any real wishlist, and a bound on what we will persist. */
const MAX_ITEMS = 200;

interface PersistedWishlist {
  v: number;
  items: Product[];
}

interface WishlistState {
  items: Product[];
  hydrated: boolean;
}

type WishlistAction =
  | { type: 'hydrate'; items: Product[] }
  | { type: 'add'; product: Product }
  | { type: 'remove'; productId: string }
  | { type: 'clear' };

const initialState: WishlistState = { items: [], hydrated: false };

function reducer(state: WishlistState, action: WishlistAction): WishlistState {
  switch (action.type) {
    case 'hydrate':
      return { items: action.items, hydrated: true };

    case 'add':
      if (state.items.some((item) => item.id === action.product.id)) return state;
      // Newest first, matching the order screen 11 lists them in.
      return { ...state, items: [action.product, ...state.items].slice(0, MAX_ITEMS) };

    case 'remove':
      return { ...state, items: state.items.filter((item) => item.id !== action.productId) };

    case 'clear':
      return { items: [], hydrated: true };

    default:
      return state;
  }
}

/** Storage is untrusted — keep only entries shaped like a product. */
function isProduct(value: unknown): value is Product {
  if (typeof value !== 'object' || value === null) return false;
  const product = value as Record<string, unknown>;
  return (
    typeof product.id === 'string' &&
    typeof product.slug === 'string' &&
    typeof product.title === 'string' &&
    typeof product.image === 'string' &&
    typeof product.price === 'number' &&
    Number.isFinite(product.price)
  );
}

function readStorage(): Product[] | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as PersistedWishlist;
    if (parsed.v !== STORAGE_VERSION || !Array.isArray(parsed.items)) return null;

    return parsed.items.filter(isProduct).slice(0, MAX_ITEMS);
  } catch {
    return null;
  }
}

export interface WishlistContextValue {
  items: Product[];
  count: number;
  hydrated: boolean;
  has: (productId: string) => boolean;
  /** Adds or removes; returns the state it moved to. */
  toggle: (product: Product) => boolean;
  remove: (productId: string) => void;
  clear: () => void;
}

const WishlistContext = createContext<WishlistContextValue | null>(null);

export function WishlistProvider({
  children,
  seed,
}: {
  children: ReactNode;
  /**
   * Saved products to show a first-time visitor, so screen 11 renders populated
   * the way the design draws it. Ignored once the customer has a list of
   * their own.
   */
  seed?: Product[];
}) {
  const [state, dispatch] = useReducer(reducer, initialState);

  useEffect(() => {
    const stored = readStorage();
    dispatch({ type: 'hydrate', items: stored ?? seed ?? [] });
    // `seed` is a server-rendered constant; re-seeding would undo edits.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!state.hydrated) return;

    try {
      const payload: PersistedWishlist = { v: STORAGE_VERSION, items: state.items };
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
    } catch {
      // Storage full or blocked — the list still works for this session.
    }
  }, [state]);

  const value = useMemo<WishlistContextValue>(() => {
    const ids = new Set(state.items.map((item) => item.id));

    return {
      items: state.items,
      count: state.items.length,
      hydrated: state.hydrated,
      has: (productId) => ids.has(productId),
      toggle: (product) => {
        const saved = ids.has(product.id);

        if (saved) {
          dispatch({ type: 'remove', productId: product.id });
          void postJson('/api/account/wishlist-remove', { productId: product.id }).catch(() => {
            // Signed-out visitors get a 401 here; the local list still holds.
          });
          return false;
        }

        dispatch({ type: 'add', product });
        return true;
      },
      remove: (productId) => {
        dispatch({ type: 'remove', productId });
        void postJson('/api/account/wishlist-remove', { productId }).catch(() => {});
      },
      clear: () => dispatch({ type: 'clear' }),
    };
  }, [state]);

  return <WishlistContext.Provider value={value}>{children}</WishlistContext.Provider>;
}

export function useWishlist(): WishlistContextValue {
  const context = useContext(WishlistContext);
  if (!context) throw new Error('useWishlist must be used inside <WishlistProvider>.');
  return context;
}
