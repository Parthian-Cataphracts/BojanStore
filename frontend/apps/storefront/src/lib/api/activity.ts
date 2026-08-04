/**
 * Notifications, tickets, reviews, wallet and coupons.
 * All per-user, so nothing here is cached across requests.
 */

import { api, useMockData } from './client';
import type {
  AwaitingReview,
  Coupon,
  MyReview,
  Notification,
  Product,
  SupportTicket,
  WalletOverview,
  WalletTransaction,
} from './types';
import {
  mockAwaitingReviews,
  mockCompareSlugs,
  mockCoupons,
  mockNotifications,
  mockRecentlyViewed,
  mockReviews,
  mockTickets,
  mockWalletTransactions,
} from '../mock/activity';
import { mockProducts } from '../mock/products';
import { mockUser } from '../mock/catalog';

// Per-user and never cached, and every one of these needs the signed-in
// customer's credential attached — see `auth` in `client.ts`.
const noStore = { cache: 'no-store', auth: true } as const;

export async function getNotifications(): Promise<Notification[]> {
  if (useMockData) return mockNotifications;
  return api.get<Notification[]>('/me/notifications', noStore);
}

export async function getTickets(): Promise<SupportTicket[]> {
  if (useMockData) return mockTickets;
  return api.get<SupportTicket[]>('/me/support/tickets', noStore);
}

export async function getMyReviews(): Promise<MyReview[]> {
  if (useMockData) return mockReviews;
  return api.get<MyReview[]>('/me/reviews', noStore);
}

export async function getAwaitingReviews(): Promise<AwaitingReview[]> {
  if (useMockData) return mockAwaitingReviews;
  return api.get<AwaitingReview[]>('/me/reviews/awaiting', noStore);
}

/**
 * The wallet screen's own state.
 *
 * The mock branch mirrors the shipped defaults: card-to-card off, so the
 * fixtures do not show a form the real API would refuse.
 */
export async function getWallet(): Promise<WalletOverview> {
  if (useMockData) {
    return {
      balance: mockUser.walletBalance,
      manualTopUpEnabled: false,
      receiptRequired: true,
      minimumTopUp: 10_000,
      maximumTopUp: 50_000_000,
      pendingTopUps: [],
    };
  }
  return api.get<WalletOverview>('/me/wallet', noStore);
}

export async function getWalletTransactions(): Promise<WalletTransaction[]> {
  if (useMockData) return mockWalletTransactions;
  return api.get<WalletTransaction[]>('/me/wallet/transactions', noStore);
}

export async function getCoupons(): Promise<Coupon[]> {
  if (useMockData) return mockCoupons;
  return api.get<Coupon[]>('/me/coupons', noStore);
}

export async function getRecentlyViewed(): Promise<Product[]> {
  if (useMockData) return mockRecentlyViewed;
  return api.get<Product[]>('/me/recently-viewed', noStore);
}

/**
 * Demo browsing history for a first-time visitor.
 *
 * Only in mock mode, for the same reason as the cart and wishlist seeds: the
 * design draws screens 57 and 90 with content in them. Real history replaces
 * it as soon as the shopper opens a product or runs a search.
 */
export async function getBrowsingSeed(): Promise<{ viewed: Product[]; terms: string[] } | null> {
  if (!useMockData) return null;

  const { mockSearchHistory } = await import('../mock/product-detail');
  return { viewed: mockRecentlyViewed, terms: mockSearchHistory };
}

/**
 * Products to compare. Slugs come from the query string so a comparison is
 * shareable; the seed list is only used when none are supplied.
 */
export async function getCompareProducts(slugs: string[]): Promise<Product[]> {
  const wanted = slugs.length > 0 ? slugs : mockCompareSlugs;

  if (useMockData) {
    return wanted
      .map((slug) => mockProducts.find((item) => item.slug === slug))
      .filter((item): item is Product => Boolean(item));
  }

  return api.get<Product[]>('/products/compare', { query: { slugs: wanted.join(',') } });
}
