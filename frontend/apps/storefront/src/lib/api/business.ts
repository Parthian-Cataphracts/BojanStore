/**
 * B2B data access — requests, quotes and gift bundles.
 * Same dual-path shape as the other API modules.
 */

import { api, useMockData } from './client';
import type { B2BRequest, GiftBundle, Quote } from './types';
import { mockB2BRequests, mockGiftBundles, mockQuotes } from '../mock/business';

// Per-user and never cached, and every one of these needs the signed-in
// customer's credential attached — see `auth` in `client.ts`.
const noStore = { cache: 'no-store', auth: true } as const;

export async function getB2BRequests(): Promise<B2BRequest[]> {
  if (useMockData) return mockB2BRequests;
  return api.get<B2BRequest[]>('/business/requests', noStore);
}

export async function getB2BRequest(id: string): Promise<B2BRequest | null> {
  if (useMockData) {
    return mockB2BRequests.find((request) => request.id === id || request.code === id) ?? null;
  }
  return api
    .get<B2BRequest>(`/business/requests/${encodeURIComponent(id)}`, noStore)
    .catch(() => null);
}

export async function getQuotes(): Promise<Quote[]> {
  if (useMockData) return mockQuotes;
  return api.get<Quote[]>('/business/quotes', noStore);
}

export async function getQuote(id: string): Promise<Quote | null> {
  if (useMockData) {
    return mockQuotes.find((quote) => quote.id === id || quote.number === id) ?? null;
  }
  return api.get<Quote>(`/business/quotes/${encodeURIComponent(id)}`, noStore).catch(() => null);
}

export async function getGiftBundles(category?: string): Promise<GiftBundle[]> {
  const all = useMockData
    ? mockGiftBundles
    : await api.get<GiftBundle[]>('/business/gift-bundles', { next: { revalidate: 3600 } });

  return category && category !== 'همه بسته‌ها'
    ? all.filter((bundle) => bundle.category === category)
    : all;
}
