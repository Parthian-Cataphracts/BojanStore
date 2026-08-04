/**
 * Account data access — orders, addresses, wishlist and the profile.
 *
 * Same dual-path shape as `catalog.ts`: the real endpoint, or a mock fallback
 * while `NEXT_PUBLIC_USE_MOCK_DATA` is on. These are all per-user, so nothing
 * here is cached across requests.
 */

import { api, useMockData } from './client';
import type {
  Address,
  Invoice,
  OrderDetail,
  OrderStatus,
  OrderSummary,
  Product,
  ReturnRequest,
  User,
} from './types';
import { mockAddresses, mockOrders, mockUser, mockWishlist } from '../mock/catalog';
import { mockInvoice, mockOrderDetails } from '../mock/orders';
import { mockReturns } from '../mock/returns';

// Per-user and never cached, and every one of these needs the signed-in
// customer's credential attached — see `auth` in `client.ts`.
const noStore = { cache: 'no-store', auth: true } as const;

export async function getCurrentUser(): Promise<User> {
  if (useMockData) return mockUser;
  return api.get<User>('/me', noStore);
}

export async function getOrders(status?: OrderStatus): Promise<OrderSummary[]> {
  if (useMockData) {
    return status ? mockOrders.filter((order) => order.status === status) : mockOrders;
  }
  return api.get<OrderSummary[]>('/me/orders', { ...noStore, query: { status } });
}

export async function getOrder(id: string): Promise<OrderDetail | null> {
  if (useMockData) {
    return mockOrderDetails.find((order) => order.id === id || order.number === id) ?? null;
  }
  return api.get<OrderDetail>(`/me/orders/${encodeURIComponent(id)}`, noStore).catch(() => null);
}

/**
 * The invoice for one of this customer's orders — screen 34.
 *
 * Null for an order that is not theirs and for one that has not been delivered
 * alike: the API answers 404 to both, on purpose, so the endpoint cannot be
 * used to learn which order numbers exist.
 */
export async function getInvoice(id: string): Promise<Invoice | null> {
  if (useMockData) return mockInvoice(id);
  return api
    .get<Invoice>(`/me/orders/${encodeURIComponent(id)}/invoice`, noStore)
    .catch(() => null);
}

export async function getAddresses(): Promise<Address[]> {
  if (useMockData) return mockAddresses;
  return api.get<Address[]>('/me/addresses', noStore);
}

export async function getAddress(id: string): Promise<Address | null> {
  if (useMockData) return mockAddresses.find((address) => address.id === id) ?? null;
  return api.get<Address>(`/me/addresses/${encodeURIComponent(id)}`, noStore).catch(() => null);
}

export async function getWishlist(): Promise<Product[]> {
  if (useMockData) return mockWishlist;
  return api.get<Product[]>('/me/wishlist', noStore);
}

/**
 * Saved products to show a first-time visitor.
 *
 * Only in mock mode, and for the same reason as the cart's seed: the design
 * draws screen 11 with items in it, and an empty-by-default demo would never
 * show that state. Against a real backend the list is whatever the customer
 * saved.
 */
export async function getWishlistSeed(): Promise<Product[] | null> {
  return useMockData ? mockWishlist : null;
}

export async function getReturns(): Promise<ReturnRequest[]> {
  if (useMockData) return mockReturns;
  return api.get<ReturnRequest[]>('/me/returns', noStore);
}

export async function getReturn(id: string): Promise<ReturnRequest | null> {
  if (useMockData) {
    return mockReturns.find((request) => request.id === id || request.code === id) ?? null;
  }
  return api.get<ReturnRequest>(`/me/returns/${encodeURIComponent(id)}`, noStore).catch(() => null);
}
