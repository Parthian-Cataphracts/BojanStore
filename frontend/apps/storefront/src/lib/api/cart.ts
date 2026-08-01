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
import {
  paymentMethods,
  shippingMethods,
  type PaymentMethod,
  type ShippingMethod,
} from '../mock/checkout';

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
 * The methods the shop actually offers, with the prices it will actually
 * charge.
 *
 * Every checkout screen used to import the fixture directly. The fixture and
 * the seeded catalogue happen to agree today, so the flow worked — but only by
 * coincidence: the moment an operator changes a shipping price, the shopper is
 * quoted the fixture's number and the order is priced with the database's. The
 * API re-prices shipping from its own record either way, so this is the read
 * that makes the two agree.
 */
export async function getShippingMethods(): Promise<ShippingMethod[]> {
  if (useMockData) return shippingMethods;

  const methods = await api
    .get<Array<{ id: string; title: string; price: number; estimate?: string; icon: string }>>(
      '/shipping-methods',
      { next: { revalidate: 3600 } },
    )
    .catch(() => []);

  return methods.map((method) => ({
    id: method.id,
    label: method.title,
    note: method.estimate ?? '',
    price: method.price,
    icon: method.icon,
  }));
}

export async function getPaymentMethods(): Promise<PaymentMethod[]> {
  if (useMockData) return paymentMethods;

  const methods = await api
    .get<Array<{ id: string; title: string; note?: string; icon: string }>>('/payment-methods', {
      next: { revalidate: 3600 },
    })
    .catch(() => []);

  return methods.map((method) => ({
    id: method.id,
    label: method.title,
    note: method.note ?? '',
    icon: method.icon,
  }));
}

/** The default (standard) shipping fee — the number the summary starts from. */
export async function getShippingFee(): Promise<number> {
  const methods = await getShippingMethods();
  return methods[0]?.price ?? 0;
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
    const known = paymentMethods.some((method) => method.id === input.paymentMethodId);
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
