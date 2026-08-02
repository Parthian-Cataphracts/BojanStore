'use client';

import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  type ReactNode,
} from 'react';
import type { Cart, CartLine, Product, ProductSku } from '@/lib/api/types';

/**
 * The cart.
 *
 * Until the .NET cart endpoints exist there is nowhere server-side to keep a
 * basket, so it lives in the browser and survives reloads through
 * `localStorage`. That is a deliberate stopgap, not the destination: every
 * mutation goes through this reducer, so moving to `POST /cart/items` means
 * changing this file and nothing else. No component touches storage directly.
 *
 * Totals are derived, never stored — a persisted total would go stale the
 * moment a price changed, and the summary has to agree with the lines.
 */

const STORAGE_KEY = 'bojan.cart.v1';
const STORAGE_VERSION = 1;
/** Per-line ceiling, so a stuck stepper cannot post an absurd quantity. */
const MAX_QUANTITY = 20;

interface PersistedCart {
  v: number;
  lines: CartLine[];
  couponCode?: string;
  discount: number;
}

interface CartState {
  lines: CartLine[];
  couponCode?: string;
  discount: number;
  /** False until the stored cart has been read — see `CartProvider`. */
  hydrated: boolean;
}

type CartAction =
  | { type: 'hydrate'; state: Omit<CartState, 'hydrated'> }
  | { type: 'add'; product: Product; quantity: number; sku?: ProductSku }
  | { type: 'setQuantity'; lineId: string; quantity: number }
  | { type: 'remove'; lineId: string }
  | { type: 'applyCoupon'; code: string; discount: number }
  | { type: 'clearCoupon' }
  | { type: 'clear' };

const initialState: CartState = { lines: [], discount: 0, hydrated: false };

function clampQuantity(quantity: number, stock?: number): number {
  const ceiling = Math.min(MAX_QUANTITY, stock && stock > 0 ? stock : MAX_QUANTITY);
  return Math.max(1, Math.min(Math.floor(quantity), ceiling));
}

function reducer(state: CartState, action: CartAction): CartState {
  switch (action.type) {
    case 'hydrate':
      return { ...action.state, hydrated: true };

    case 'add': {
      const { product, quantity, sku } = action;
      const stock = sku?.stock ?? product.stock;
      // A different SKU of the same product is a different line — the design
      // shows one row per product only when there is nothing else to tell
      // two lines apart.
      const existing = state.lines.find(
        (line) => line.productId === product.id && line.skuId === sku?.id,
      );

      if (existing) {
        return {
          ...state,
          lines: state.lines.map((line) =>
            line === existing
              ? { ...line, quantity: clampQuantity(line.quantity + quantity, stock) }
              : line,
          ),
        };
      }

      const line: CartLine = {
        id: sku ? `line-${product.id}-${sku.id}` : `line-${product.id}`,
        productId: product.id,
        ...(sku ? { skuId: sku.id } : null),
        slug: product.slug,
        title: product.title,
        brand: product.brand,
        image: product.image,
        unitPrice: sku?.price ?? product.price,
        ...(product.compareAtPrice ? { compareAtPrice: product.compareAtPrice } : null),
        quantity: clampQuantity(quantity, stock),
      };

      return { ...state, lines: [...state.lines, line] };
    }

    case 'setQuantity':
      return {
        ...state,
        lines: state.lines.map((line) =>
          line.id === action.lineId ? { ...line, quantity: clampQuantity(action.quantity) } : line,
        ),
      };

    case 'remove': {
      const lines = state.lines.filter((line) => line.id !== action.lineId);
      // An empty basket cannot carry a coupon into checkout.
      return lines.length === 0
        ? { ...state, lines, discount: 0, couponCode: undefined }
        : { ...state, lines };
    }

    case 'applyCoupon':
      return { ...state, couponCode: action.code, discount: Math.max(0, action.discount) };

    case 'clearCoupon':
      return { ...state, couponCode: undefined, discount: 0 };

    case 'clear':
      return { lines: [], discount: 0, hydrated: true };

    default:
      return state;
  }
}

/** Reject anything that is not shaped like a cart line — storage is untrusted. */
function isCartLine(value: unknown): value is CartLine {
  if (typeof value !== 'object' || value === null) return false;
  const line = value as Record<string, unknown>;
  return (
    typeof line.id === 'string' &&
    typeof line.productId === 'string' &&
    (line.skuId === undefined || typeof line.skuId === 'string') &&
    typeof line.slug === 'string' &&
    typeof line.title === 'string' &&
    typeof line.image === 'string' &&
    typeof line.unitPrice === 'number' &&
    Number.isFinite(line.unitPrice) &&
    typeof line.quantity === 'number' &&
    Number.isFinite(line.quantity)
  );
}

function readStorage(): Omit<CartState, 'hydrated'> | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as PersistedCart;
    if (parsed.v !== STORAGE_VERSION || !Array.isArray(parsed.lines)) return null;

    return {
      lines: parsed.lines.filter(isCartLine).map((line) => ({
        ...line,
        quantity: clampQuantity(line.quantity),
      })),
      ...(typeof parsed.couponCode === 'string' ? { couponCode: parsed.couponCode } : null),
      discount: typeof parsed.discount === 'number' && parsed.discount > 0 ? parsed.discount : 0,
    };
  } catch {
    // Corrupt or unavailable storage (private mode, quota) — start clean.
    return null;
  }
}

export interface CartContextValue {
  cart: Cart;
  /** Total units, for the header count. */
  count: number;
  /** False during the first paint, before storage has been read. */
  hydrated: boolean;
  addItem: (product: Product, quantity?: number, sku?: ProductSku) => void;
  setQuantity: (lineId: string, quantity: number) => void;
  removeItem: (lineId: string) => void;
  applyCoupon: (code: string, discount: number) => void;
  clearCoupon: () => void;
  clear: () => void;
}

const CartContext = createContext<CartContextValue | null>(null);

export interface CartProviderProps {
  children: ReactNode;
  /**
   * Flat shipping fee for the basket. Comes from the server so the number is
   * not duplicated in client code.
   */
  shipping: number;
  /**
   * Basket to seed a first-time visitor with. Used in mock mode so the cart and
   * checkout screens render populated, the way the design draws them. Ignored
   * once the shopper has a cart of their own.
   */
  seed?: Cart;
}

export function CartProvider({ children, shipping, seed }: CartProviderProps) {
  const [state, dispatch] = useReducer(reducer, initialState);

  // Storage is read after mount, never during render: the server has no
  // `localStorage`, and reading it inline would make the first client render
  // disagree with the server HTML and blow up hydration.
  useEffect(() => {
    const stored = readStorage();

    if (stored) {
      dispatch({ type: 'hydrate', state: stored });
      return;
    }

    dispatch({
      type: 'hydrate',
      state: seed
        ? {
            lines: seed.lines,
            ...(seed.couponCode ? { couponCode: seed.couponCode } : null),
            discount: seed.discount,
          }
        : { lines: [], discount: 0 },
    });
    // `seed` is a server-rendered constant; re-seeding on every change would
    // undo the shopper's edits.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!state.hydrated) return;

    try {
      const payload: PersistedCart = {
        v: STORAGE_VERSION,
        lines: state.lines,
        ...(state.couponCode ? { couponCode: state.couponCode } : null),
        discount: state.discount,
      };
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
    } catch {
      // Storage full or blocked — the cart still works for this session.
    }
  }, [state]);

  const value = useMemo<CartContextValue>(() => {
    const subtotal = state.lines.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0);
    const empty = state.lines.length === 0;
    // A discount can never exceed the goods, and an empty basket owes nothing.
    const discount = empty ? 0 : Math.min(state.discount, subtotal);
    const shippingFee = empty ? 0 : shipping;

    const cart: Cart = {
      id: 'local',
      lines: state.lines,
      subtotal,
      discount,
      shipping: shippingFee,
      total: subtotal - discount + shippingFee,
      ...(state.couponCode && !empty ? { couponCode: state.couponCode } : null),
    };

    return {
      cart,
      count: state.lines.reduce((sum, line) => sum + line.quantity, 0),
      hydrated: state.hydrated,
      addItem: (product, quantity = 1, sku) => dispatch({ type: 'add', product, quantity, sku }),
      setQuantity: (lineId, quantity) => dispatch({ type: 'setQuantity', lineId, quantity }),
      removeItem: (lineId) => dispatch({ type: 'remove', lineId }),
      applyCoupon: (code, discountValue) =>
        dispatch({ type: 'applyCoupon', code, discount: discountValue }),
      clearCoupon: () => dispatch({ type: 'clearCoupon' }),
      clear: () => dispatch({ type: 'clear' }),
    };
  }, [state, shipping]);

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart(): CartContextValue {
  const context = useContext(CartContext);
  if (!context) throw new Error('useCart must be used inside <CartProvider>.');
  return context;
}
