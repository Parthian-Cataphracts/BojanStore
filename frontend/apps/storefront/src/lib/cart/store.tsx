'use client';

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  useRef,
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
/**
 * Per-line ceiling, so a stuck stepper cannot post an absurd quantity.
 *
 * Exported because the cart's stepper has to be given the same number: its own
 * default is 99, and a control that counts past what the reducer will store is
 * a control that stops responding without saying why.
 */
export const MAX_CART_QUANTITY = 20;

/**
 * Marks the applied discount as no longer priced for this basket.
 *
 * The amount was stored and left alone through every line change, so a coupon
 * worth 100,000 on a 500,000 basket stayed worth 100,000 after the basket
 * dropped to 50,000 — the summary showed a discount larger than the goods, and
 * the total the shopper agreed to was not the total the server would charge.
 * The code is kept: the shopper did apply it, and it is re-priced against the
 * new lines rather than made them type it again. Only the number goes.
 */
function repriced(state: CartState): CartState {
  return state.couponCode ? { ...state, discount: 0 } : state;
}

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
  | { type: 'changeQuantity'; lineId: string; delta: number }
  | { type: 'remove'; lineId: string }
  | { type: 'applyCoupon'; code: string; discount: number }
  | { type: 'clearCoupon' }
  | { type: 'reprice'; lines: PricedLine[] }
  | { type: 'clear' };

/** What the catalogue says a stored line costs today. */
export interface PricedLine {
  slug: string;
  skuId?: string;
  price: number;
  /** The list price, when the catalogue currently has it on sale. */
  compareAtPrice?: number;
  /** Units on hand. Zero is sold out, and is a value rather than an absence. */
  stock: number;
}

/**
 * Whether this line can still be bought.
 *
 * Sold out is `stock === 0` — the same test the product card, the product page
 * and the compare table already make, so a shopper is never told a thing is
 * available on one screen and not on another.
 *
 * An absent `stock` is not sold out: it means nothing has ever told this line
 * what its stock is — a line stored before the field existed, or a product the
 * shop does not count. Both mean "only the per-line ceiling applies", which is
 * what the basket did for every line before any of this.
 */
export function isLineAvailable(line: Pick<CartLine, 'stock'>): boolean {
  return line.stock !== 0;
}

const initialState: CartState = { lines: [], discount: 0, hydrated: false };

function clampQuantity(quantity: number, stock?: number): number {
  const ceiling = Math.min(MAX_CART_QUANTITY, stock && stock > 0 ? stock : MAX_CART_QUANTITY);
  // A non-finite quantity survived `Math.floor`/`min`/`max` and poisoned every
  // number derived from it — the line, the subtotal, the total and the header
  // count all rendered "NaN", and the line was then dropped on the next visit
  // because storage round-trips NaN as null.
  if (!Number.isFinite(quantity)) return 1;
  return Math.max(1, Math.min(Math.floor(quantity), ceiling));
}

/**
 * Identifies a line for re-pricing: a product, or one chosen combination of it.
 *
 * The pipe is safe because neither half can contain one — a slug is generated
 * and a SKU id is a GUID.
 */
function lineKey(line: { slug: string; skuId?: string }): string {
  return `${line.slug}|${line.skuId ?? ''}`;
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
        return repriced({
          ...state,
          lines: state.lines.map((line) =>
            line === existing
              ? { ...line, quantity: clampQuantity(line.quantity + quantity, stock) }
              : line,
          ),
        });
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
        // Carried on the line so the cart's own stepper can clamp to it. Adding
        // to the basket always did; changing the quantity afterwards did not,
        // so a shopper could take a two-in-stock product to twenty from the
        // cart page and be refused four screens later at the moment of payment.
        ...(stock > 0 ? { stock } : null),
      };

      return repriced({ ...state, lines: [...state.lines, line] });
    }

    case 'setQuantity':
      return repriced({
        ...state,
        lines: state.lines.map((line) =>
          line.id === action.lineId
            ? { ...line, quantity: clampQuantity(action.quantity, line.stock) }
            : line,
        ),
      });

    /*
      The same as `setQuantity`, except the caller says how much to move rather
      than where to land.

      A stepper works out `value + 1` from the value it was last rendered with,
      so two presses in one frame both ask for the same number and the second
      does nothing. Adding a delta here reads the quantity as it is when the
      action arrives, so presses accumulate however fast they come — which is
      the whole point of a control that replaces «add to basket» and is meant
      to be pressed more than once.
    */
    case 'changeQuantity':
      return repriced({
        ...state,
        lines: state.lines.map((line) =>
          line.id === action.lineId
            ? { ...line, quantity: clampQuantity(line.quantity + action.delta, line.stock) }
            : line,
        ),
      });

    case 'remove': {
      const lines = state.lines.filter((line) => line.id !== action.lineId);
      // An empty basket cannot carry a coupon into checkout.
      return lines.length === 0
        ? { ...state, lines, discount: 0, couponCode: undefined }
        : repriced({ ...state, lines });
    }

    case 'applyCoupon':
      return { ...state, couponCode: action.code, discount: Math.max(0, action.discount) };

    case 'clearCoupon':
      return { ...state, couponCode: undefined, discount: 0 };

    case 'reprice': {
      const fresh = new Map(
        action.lines.map((line) => [lineKey(line), line]),
      );

      // A line whose product no longer resolves is dropped rather than kept at
      // its old price: the catalogue is the authority on what is for sale, and
      // an order containing it would be refused anyway. A product that still
      // exists and has sold out is *not* that case — it comes back with a stock
      // of zero and stays, marked and uncharged, because taking something out
      // of a basket without saying so is worse than showing it unavailable.
      const lines = state.lines
        .map((line): CartLine | null => {
          const current = fresh.get(lineKey(line));
          if (!current) return null;

          return {
            ...line,
            unitPrice: current.price,
            /*
              Written every time, cleared included.

              On sale only while the list price is genuinely above what is being
              charged; a stale or equal figure is not a discount and is not
              drawn as one. Adding the key only when there is a sale — the usual
              shape here — would leave a product that came *off* sale carrying
              the old list price it was added with, still advertising a discount
              the shop had stopped giving. `undefined` drops out of the JSON on
              the way to storage, so nothing stale is kept there either.
            */
            compareAtPrice:
              current.compareAtPrice && current.compareAtPrice > current.price
                ? current.compareAtPrice
                : undefined,
            // Always written, zero included — that is the whole signal for sold
            // out. Writing it only when positive is what left a line that had
            // just sold out carrying the stock it was added with, so it looked
            // available and its stepper still counted up to it.
            stock: current.stock,
            // A sold-out line keeps the quantity the shopper chose rather than
            // being clamped to zero, so it reads as "one of these, unavailable"
            // and goes back to being that if the shop restocks.
            quantity: current.stock > 0 ? clampQuantity(line.quantity, current.stock) : line.quantity,
          };
        })
        .filter((line): line is CartLine => line !== null);

      const changed =
        lines.length !== state.lines.length ||
        lines.some((line, index) => {
          const before = state.lines[index]!;
          return (
            line.unitPrice !== before.unitPrice ||
            line.quantity !== before.quantity ||
            line.stock !== before.stock ||
            line.compareAtPrice !== before.compareAtPrice
          );
        });

      if (!changed) return state;

      return lines.length === 0
        ? { ...state, lines, discount: 0, couponCode: undefined }
        : repriced({ ...state, lines });
    }

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
    // Positive, not merely finite. A tampered store with a negative price
    // rendered a negative subtotal and a negative discount line beside it.
    line.unitPrice > 0 &&
    typeof line.quantity === 'number' &&
    Number.isFinite(line.quantity) &&
    (line.stock === undefined || typeof line.stock === 'number')
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
        quantity: clampQuantity(line.quantity, line.stock),
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
  /**
   * The lines the order is actually made of — everything still in stock.
   *
   * Exposed rather than re-derived at each call site: the checkout submits it,
   * the coupon is priced against it and the summary counts it, and three copies
   * of "which lines count" is how one of them comes to disagree with the total
   * sitting next to it.
   */
  purchasableLines: CartLine[];
  /**
   * Re-reads every line's price and stock from the catalogue.
   *
   * Runs once on hydration by itself. The checkout calls it again on the way
   * in, because a tab left open while a sale started or a size sold out would
   * otherwise carry the figures it loaded with all the way to the pay button —
   * and the API prices the real order from the catalogue regardless, so the
   * disagreement surfaced as a refused payment rather than as a changed total.
   */
  refresh: () => void;
  /** False during the first paint, before storage has been read. */
  hydrated: boolean;
  addItem: (product: Product, quantity?: number, sku?: ProductSku) => void;
  setQuantity: (lineId: string, quantity: number) => void;
  /** Moves a line's quantity by `delta`, resolved against the basket as it is. */
  changeQuantity: (lineId: string, delta: number) => void;
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

  /*
    What the basket costs, and what of it is still in stock, right now.

    A line keeps whatever the product cost on the day it was added, so a basket
    left for a week showed last week's prices and a line for something since
    sold out still offered it — while the API re-prices every order from the
    catalogue when it is placed. The shopper was agreeing to one number and
    being charged another.

    Read through a ref rather than taken as a dependency: the identity of this
    function has to stay stable, or every screen that refreshes on mount would
    refresh again on the render its own answer causes.
  */
  const linesRef = useRef(state.lines);
  linesRef.current = state.lines;

  const refresh = useCallback(async (signal?: AbortSignal) => {
    const lines = linesRef.current;
    if (lines.length === 0) return;

    try {
      const response = await fetch('/api/cart/prices', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          lines: lines.map((line) => ({
            slug: line.slug,
            ...(line.skuId ? { skuId: line.skuId } : null),
          })),
        }),
        ...(signal ? { signal } : null),
      });

      if (!response.ok || signal?.aborted) return;

      const { lines: priced } = (await response.json()) as { lines: PricedLine[] };
      if (priced.length > 0) dispatch({ type: 'reprice', lines: priced });
    } catch {
      // The basket keeps what it has. A stale price is worse than a fresh
      // one, but emptying somebody's cart over a failed request is worse
      // than both — and the order itself is priced by the server regardless.
    }
  }, []);

  // Once per mount, after storage has been read. The checkout asks again when
  // it opens, which is the moment the numbers actually have to be right.
  useEffect(() => {
    if (!state.hydrated) return;

    const controller = new AbortController();

    (async () => {
      try {
        await refresh(controller.signal);
      } catch {
        // The basket keeps what it has. A stale price is worse than a fresh
        // one, but emptying somebody's cart over a failed request is worse
        // than both — and the order itself is priced by the server regardless.
      }
    })();

    return () => controller.abort();
    // Deliberately only on hydration: re-running whenever the lines change
    // would fire on every tap of a quantity stepper. `refresh` is stable.
  }, [state.hydrated, refresh]);

  // Re-prices a coupon whose amount `repriced` cleared, and drops one the new
  // basket no longer qualifies for — a minimum-spend code stops applying the
  // moment the basket falls under it, and the shopper has to be told that
  // before the payment screen rather than after.
  //
  // Here rather than in the two coupon forms: the rule belongs with the cart,
  // and a screen that happens not to render a coupon field must not be a screen
  // where the discount silently stops being checked.
  useEffect(() => {
    if (!state.hydrated || !state.couponCode || state.discount > 0) return;

    // Priced against what can be bought, like every other total. A coupon
    // costed on a sold-out line is a discount off something the order will not
    // contain, and the API — which prices the real order from the catalogue —
    // would come back with a different number at the moment of payment.
    const eligible = state.lines.filter(isLineAvailable);
    if (eligible.length === 0) return;

    const controller = new AbortController();
    const code = state.couponCode;
    const subtotal = eligible.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0);

    (async () => {
      try {
        const response = await fetch('/api/cart/coupon', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            code,
            subtotal,
            lines: eligible.map((line) => ({
              productId: line.productId,
              quantity: line.quantity,
              ...(line.skuId ? { skuId: line.skuId } : null),
            })),
          }),
          signal: controller.signal,
        });

        if (controller.signal.aborted) return;

        if (!response.ok) {
          dispatch({ type: 'clearCoupon' });
          return;
        }

        const applied = (await response.json()) as { code: string; discount: number };
        dispatch({ type: 'applyCoupon', code: applied.code, discount: applied.discount });
      } catch {
        // A failed round trip leaves the code applied and the amount at zero,
        // which understates the discount rather than overstating it. The next
        // basket change tries again, and the server prices the order for real.
      }
    })();

    return () => controller.abort();
  }, [state.hydrated, state.couponCode, state.discount, state.lines]);

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
    /*
      Only what can actually be bought is counted.

      A line that has sold out since it was added stays in the basket so the
      shopper can see what happened to it, but it is not goods any more: it was
      still being summed into the subtotal, still carrying the coupon's
      percentage and still in the figure on the pay button, for something the
      checkout would refuse. Every number below is over this list; the screens
      render `lines`, which still holds both kinds.
    */
    const purchasable = state.lines.filter(isLineAvailable);

    const subtotal = purchasable.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0);
    // Empty of *purchasable* lines: a basket holding nothing but sold-out items
    // owes nothing and must not be charged delivery on it.
    const empty = purchasable.length === 0;
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
      /*
        Distinct lines, not units.

        The badge sat on the cart icon reading the total quantity, so adding
        three of one notebook made it say ۳ — which reads as three things in the
        basket, and the basket has one. Every shop this one is modelled on
        counts lines here, and the number people check the badge against is
        "how many things am I buying", not "how many units".
      */
      count: state.lines.length,
      /** Lines the checkout can actually order — see `purchasable` above. */
      purchasableLines: purchasable,
      refresh: () => void refresh(),
      hydrated: state.hydrated,
      addItem: (product, quantity = 1, sku) => dispatch({ type: 'add', product, quantity, sku }),
      setQuantity: (lineId, quantity) => dispatch({ type: 'setQuantity', lineId, quantity }),
      changeQuantity: (lineId, delta) => dispatch({ type: 'changeQuantity', lineId, delta }),
      removeItem: (lineId) => dispatch({ type: 'remove', lineId }),
      applyCoupon: (code, discountValue) =>
        dispatch({ type: 'applyCoupon', code, discount: discountValue }),
      clearCoupon: () => dispatch({ type: 'clearCoupon' }),
      /*
        Emptied in storage now, not after the next render.

        Everything else here persists through the effect below, which is fine
        because the page stays put. This one is called at the moment the order
        is placed, and the very next statement in `PlaceOrderButton` is
        `window.location.href = paymentUrl` — a full navigation that begins
        immediately. The dispatch had not been rendered yet, so the effect that
        writes `localStorage` never ran, and the shopper came back from the
        gateway to a basket still holding everything they had just bought.
        Cash on delivery escaped it only because `router.push` is a client
        navigation that lets React finish first.
      */
      clear: () => {
        dispatch({ type: 'clear' });

        try {
          window.localStorage.setItem(
            STORAGE_KEY,
            JSON.stringify({ v: STORAGE_VERSION, lines: [], discount: 0 } satisfies PersistedCart),
          );
        } catch {
          // Storage full or blocked. The dispatch above still empties the
          // basket for as long as this tab lives.
        }
      },
    };
  }, [state, shipping, refresh]);

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart(): CartContextValue {
  const context = useContext(CartContext);
  if (!context) throw new Error('useCart must be used inside <CartProvider>.');
  return context;
}
