/**
 * Cart and checkout data access.
 *
 * Same dual-path shape as `catalog.ts`. The basket itself lives in the browser
 * for now (see `lib/cart/store`), but everything the basket needs from the
 * server — the shipping fee, coupon validation, order placement — is resolved
 * here so no component holds a business rule of its own.
 */

import { api, ApiError, useMockData } from './client';
import type { Cart, CartLine, OrderDetail } from './types';
import { mockCart } from '../mock/catalog';
import { mockOrderDetails } from '../mock/orders';
import { paymentMethods, shippingMethods } from '../mock/checkout';

// Per-user and never cached, and every one of these needs the signed-in
// customer's credential attached — see `auth` in `client.ts`.
const noStore = { cache: 'no-store', auth: true } as const;

export interface CouponResult {
  code: string;
  /** Absolute discount in Toman. */
  discount: number;
}

export interface PlacedOrder {
  orderNumber: string;
  /** Gateway URL to send the customer to; absent for cash on delivery. */
  paymentUrl?: string;
}

export interface PlaceOrderInput {
  lines: Array<Pick<CartLine, 'productId' | 'quantity'>>;
  addressId: string;
  shippingMethodId: string;
  paymentMethodId: string;
  couponCode?: string;
  note?: string;
}

/**
 * A shipping tier as the checkout screens render it.
 *
 * The API's own shape, which the fixtures are mapped into rather than the
 * other way round: `id` is the wire code the order submits, and `price` is the
 * number the order will actually be charged.
 */
export interface CheckoutShippingMethod {
  id: string;
  title: string;
  note?: string;
  price: number;
  icon: string;
}

export interface CheckoutPaymentMethod {
  id: string;
  title: string;
  note?: string;
  icon: string;
}

/**
 * The shipping tiers on offer.
 *
 * Read from the API rather than from the fixture the checkout screens used to
 * render. Two things were wrong with the fixture: its prices are constants, so
 * an operator who repriced a tier got a checkout that displayed one figure and
 * an order that charged another — the API prices shipping from its own row —
 * and a tier deactivated in the panel still appeared, only to be refused at
 * placement.
 */
export async function getShippingMethods(): Promise<CheckoutShippingMethod[]> {
  if (useMockData) {
    return shippingMethods.map((method) => ({
      id: method.id,
      title: method.label,
      note: method.note,
      price: method.price,
      icon: method.icon,
    }));
  }

  const methods = await api
    .get<Array<{ id: string; title: string; price: number; estimate?: string; icon: string }>>(
      '/shipping-methods',
      { next: { revalidate: 3600 } },
    )
    .catch(() => []);

  return methods.map((method) => ({
    id: method.id,
    title: method.title,
    ...(method.estimate ? { note: method.estimate } : null),
    price: method.price,
    icon: method.icon,
  }));
}

export async function getPaymentMethods(): Promise<CheckoutPaymentMethod[]> {
  if (useMockData) {
    return paymentMethods.map((method) => ({
      id: method.id,
      title: method.label,
      note: method.note,
      icon: method.icon,
    }));
  }

  const methods = await api
    .get<Array<{ id: string; title: string; note?: string; icon: string }>>('/payment-methods', {
      next: { revalidate: 3600 },
    })
    .catch(() => []);

  return methods.map((method) => ({
    id: method.id,
    title: method.title,
    ...(method.note ? { note: method.note } : null),
    icon: method.icon,
  }));
}

/** The default (standard) shipping fee — the number the summary starts from. */
export async function getShippingFee(): Promise<number> {
  return (await getShippingMethods())[0]?.price ?? 0;
}

/**
 * Basket to show a first-time visitor.
 *
 * Only in mock mode: the design draws the cart and checkout screens with items
 * in them, and an empty-by-default demo would never show that state. Against a
 * real backend the shopper's cart is whatever they put in it.
 */
export async function getCartSeed(): Promise<Cart | null> {
  return useMockData ? mockCart : null;
}

/** Screen 76 — apply a discount code. */
export async function validateCoupon(
  code: string,
  subtotal: number,
  lines: Array<Pick<CartLine, 'productId' | 'quantity'>> = [],
): Promise<CouponResult> {
  const normalized = code.trim().toUpperCase();

  if (useMockData) {
    // The one code the design shows, worth the amount the design's summary uses.
    if (normalized !== 'BOJAN10') {
      throw new Error('کد تخفیف معتبر نیست یا منقضی شده است.');
    }
    // Never let a fixed-value coupon exceed the goods it discounts.
    return { code: normalized, discount: Math.min(120_000, subtotal) };
  }

  // The backend re-prices the coupon from these lines — it never trusts the
  // client's `subtotal`. Sending no lines here would price it against an
  // empty basket and reject every minimum-order coupon.
  return api.post<CouponResult>(
    '/cart/coupon',
    { code: normalized, lines },
    noStore,
  );
}

/** Screens 08 and 77-78 — place the order. */
export async function placeOrder(input: PlaceOrderInput): Promise<PlacedOrder> {
  if (useMockData) {
    const known = (await getPaymentMethods()).some((method) => method.id === input.paymentMethodId);
    if (!known) throw new Error('روش پرداخت انتخاب‌شده معتبر نیست.');

    // A plausible order number in the format the order screens render.
    const serial = String(Math.floor(Math.random() * 900_000) + 100_000);
    return { orderNumber: `BJ-${serial}` };
  }

  return api.post<PlacedOrder>('/orders', input, noStore);
}

/**
 * Screen 30 — guest order tracking. Public and rate-limited server-side: no
 * session, so no `auth`. Requires both the order number and the phone it was
 * placed with — the API 404s rather than confirming a number alone exists.
 */
export async function trackOrder(number: string, phone: string): Promise<OrderDetail | null> {
  if (useMockData) {
    const needle = number.replace(/^#/, '').toUpperCase();
    return mockOrderDetails.find((order) => order.number.toUpperCase() === needle) ?? null;
  }

  try {
    return await api.get<OrderDetail>('/orders/track', {
      cache: 'no-store',
      query: { number, phone },
    });
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}
