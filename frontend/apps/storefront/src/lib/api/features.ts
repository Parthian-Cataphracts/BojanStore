/**
 * Delivered KNIGHT features, read by the storefront.
 *
 * Each of these is an installed feature the merchant bought; the store exposes it
 * through its `/api/features/<prefix>/…` proxy (anonymous prefixes, so no shopper
 * sign-in is needed to read them). Every call is best-effort: if the feature is
 * not installed, is down, or answers oddly, the helper returns an empty result
 * rather than throwing, so a product page never breaks because an add-on is off.
 */

import { api } from './client';

export interface FeatureReview {
  rating: number;
  body: string | null;
  reply: string | null;
  at: string;
}

export interface ProductReviews {
  productId: string;
  count: number;
  average: number;
  reviews: FeatureReview[];
}

/** Approved reviews and the average for one product (reviews-ratings). */
export async function getFeatureReviews(productId: string): Promise<ProductReviews> {
  const empty: ProductReviews = { productId, count: 0, average: 0, reviews: [] };
  if (!productId) return empty;
  try {
    return await api.get<ProductReviews>('/features/reviews-public/product', {
      query: { productId },
    });
  } catch {
    return empty;
  }
}

export interface Recommendation {
  productId: string;
  title: string;
  slug: string;
  score: number;
  reason: string;
}

/** "Customers who bought this also bought…" (ai-recommendations). */
export async function getRecommendations(productId: string, limit = 6): Promise<Recommendation[]> {
  try {
    const data = await api.get<{ recommendations: Recommendation[] }>('/features/recommend/recommend', {
      query: { productId, limit },
    });
    return data.recommendations ?? [];
  } catch {
    return [];
  }
}

export interface SearchHit {
  productId: string;
  title: string;
  slug: string;
  price: number;
  stock: number;
}

/** Ranked full-text search over the pushed catalogue (advanced-search). */
export async function searchProducts(q: string, limit = 24): Promise<SearchHit[]> {
  if (!q.trim()) return [];
  try {
    const data = await api.get<{ results: SearchHit[] }>('/features/search/search', {
      query: { q, limit },
    });
    return data.results ?? [];
  } catch {
    return [];
  }
}

export interface Branch {
  id: string;
  name: string;
  address: string;
  city: string;
  phone: string;
  lat: number | null;
  lng: number | null;
  hours: Record<string, string> | Record<string, never>;
  pickup: boolean;
}

/** The shop's physical branches (multi-location). */
export async function getBranches(pickupOnly = false): Promise<Branch[]> {
  try {
    const data = await api.get<{ locations: Branch[] }>('/features/locations/locations', {
      query: pickupOnly ? { pickup: '1' } : undefined,
    });
    return data.locations ?? [];
  } catch {
    return [];
  }
}
