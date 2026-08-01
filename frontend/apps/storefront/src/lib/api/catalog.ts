/**
 * Catalogue data access.
 *
 * Every function has two paths: the real `/api/...` call against the .NET
 * backend, and a mock fallback used while `NEXT_PUBLIC_USE_MOCK_DATA` is on.
 * Pages only ever import from here, so flipping the env var is the whole
 * migration — no page needs to change.
 */

import { api, useMockData } from './client';
import type {
  Brand,
  Category,
  Paged,
  Product,
  ProductQuery,
  ProductQuestion,
  ProductReview,
  ProductVariantAxis,
  RatingBreakdown,
} from './types';
import { mockCategories } from '../mock/catalog';
import { mockProducts } from '../mock/products';
import {
  mockProductQuestions,
  mockProductReviews,
  mockRatingBreakdown,
  mockVariantAxes,
} from '../mock/product-detail';

const LIST_REVALIDATE = 300;

function filterMock({ items, query }: { items: Product[]; query: ProductQuery }): Product[] {
  let result = items;

  if (query.category) result = result.filter((p) => p.categorySlug === query.category);
  if (query.brand) result = result.filter((p) => p.brandSlug === query.brand);
  if (query.inStockOnly) result = result.filter((p) => p.stock > 0);
  if (query.minPrice !== undefined) result = result.filter((p) => p.price >= query.minPrice!);
  if (query.maxPrice !== undefined) result = result.filter((p) => p.price <= query.maxPrice!);

  if (query.search) {
    const needle = query.search.trim();
    result = result.filter(
      (p) => p.title.includes(needle) || p.brand.includes(needle) || p.categoryName.includes(needle),
    );
  }

  switch (query.sort) {
    case 'price-asc':
      result = [...result].sort((a, b) => a.price - b.price);
      break;
    case 'price-desc':
      result = [...result].sort((a, b) => b.price - a.price);
      break;
    case 'rating':
      result = [...result].sort((a, b) => b.rating - a.rating);
      break;
    case 'bestselling':
      result = [...result].sort((a, b) => Number(b.isBestseller) - Number(a.isBestseller));
      break;
    case 'newest':
      result = [...result].sort((a, b) => Number(b.isNew) - Number(a.isNew));
      break;
    default:
      break;
  }

  return result;
}

export async function getProducts(query: ProductQuery = {}): Promise<Paged<Product>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? 24;

  if (useMockData) {
    const filtered = filterMock({ items: mockProducts, query });
    return {
      items: filtered.slice((page - 1) * pageSize, page * pageSize),
      page,
      pageSize,
      total: filtered.length,
    };
  }

  return api.get<Paged<Product>>('/products', {
    query: { ...query, page, pageSize },
    next: { revalidate: LIST_REVALIDATE, tags: ['products'] },
  });
}

export async function getProduct(slug: string): Promise<Product | null> {
  if (useMockData) {
    return mockProducts.find((p) => p.slug === slug) ?? null;
  }

  return api
    .get<Product>(`/products/${encodeURIComponent(slug)}`, {
      next: { revalidate: LIST_REVALIDATE, tags: ['products', `product:${slug}`] },
    })
    .catch(() => null);
}

export async function getRelatedProducts(slug: string, limit = 4): Promise<Product[]> {
  if (useMockData) {
    const current = mockProducts.find((p) => p.slug === slug);
    if (!current) return mockProducts.slice(0, limit);
    return mockProducts
      .filter((p) => p.slug !== slug && p.categorySlug === current.categorySlug)
      .slice(0, limit);
  }

  return api.get<Product[]>(`/products/${encodeURIComponent(slug)}/related`, {
    query: { limit },
    next: { revalidate: LIST_REVALIDATE },
  });
}

export async function getProductReviews(slug: string): Promise<ProductReview[]> {
  if (useMockData) return mockProductReviews;

  return api.get<ProductReview[]>(`/products/${encodeURIComponent(slug)}/reviews`, {
    next: { revalidate: LIST_REVALIDATE, tags: [`product:${slug}:reviews`] },
  });
}

export async function getRatingBreakdown(slug: string): Promise<RatingBreakdown> {
  if (useMockData) return mockRatingBreakdown;

  return api.get<RatingBreakdown>(`/products/${encodeURIComponent(slug)}/rating`, {
    next: { revalidate: LIST_REVALIDATE, tags: [`product:${slug}:reviews`] },
  });
}

export async function getProductQuestions(slug: string): Promise<ProductQuestion[]> {
  if (useMockData) return mockProductQuestions;

  return api.get<ProductQuestion[]>(`/products/${encodeURIComponent(slug)}/questions`, {
    next: { revalidate: LIST_REVALIDATE, tags: [`product:${slug}:questions`] },
  });
}

export async function getVariantAxes(slug: string): Promise<ProductVariantAxis[]> {
  if (useMockData) return mockVariantAxes;

  return api.get<ProductVariantAxis[]>(`/products/${encodeURIComponent(slug)}/variants`, {
    next: { revalidate: LIST_REVALIDATE },
  });
}

export async function getCategories(): Promise<Category[]> {
  if (useMockData) return mockCategories;

  return api.get<Category[]>('/categories', {
    next: { revalidate: 3600, tags: ['categories'] },
  });
}

export async function getCategory(slug: string): Promise<Category | null> {
  if (useMockData) return mockCategories.find((c) => c.slug === slug) ?? null;

  return api
    .get<Category>(`/categories/${encodeURIComponent(slug)}`, {
      next: { revalidate: 3600, tags: ['categories'] },
    })
    .catch(() => null);
}

export async function getBrands(): Promise<Brand[]> {
  if (useMockData) {
    const counts = new Map<string, Brand>();
    for (const product of mockProducts) {
      const existing = counts.get(product.brandSlug);
      if (existing) existing.productCount += 1;
      else counts.set(product.brandSlug, { slug: product.brandSlug, name: product.brand, productCount: 1 });
    }
    return [...counts.values()].sort((a, b) => b.productCount - a.productCount);
  }

  return api.get<Brand[]>('/brands', { next: { revalidate: 3600 } });
}

/** Homepage rails. */
export async function getNewArrivals(limit = 8): Promise<Product[]> {
  const { items } = await getProducts({ sort: 'newest', pageSize: limit });
  return items;
}

export async function getBestsellers(limit = 8): Promise<Product[]> {
  const { items } = await getProducts({ sort: 'bestselling', pageSize: limit });
  return items;
}
