/**
 * Collections, brand profiles and magazine articles.
 * Same dual-path shape as `catalog.ts`.
 */

import { api, useMockData } from './client';
import type { Article, Brand, Collection, Product } from './types';
import { mockArticles, mockBrands, mockCollections } from '../mock/editorial';
import { mockProducts } from '../mock/products';

const EDITORIAL_REVALIDATE = 3600;

export async function getCollections(): Promise<Collection[]> {
  if (useMockData) return mockCollections;
  return api.get<Collection[]>('/collections', {
    next: { revalidate: EDITORIAL_REVALIDATE, tags: ['collections'] },
  });
}

export async function getCollection(slug: string): Promise<Collection | null> {
  if (useMockData) return mockCollections.find((c) => c.slug === slug) ?? null;
  return api
    .get<Collection>(`/collections/${encodeURIComponent(slug)}`, {
      next: { revalidate: EDITORIAL_REVALIDATE, tags: ['collections'] },
    })
    .catch(() => null);
}

/** Resolve a collection's product slugs to full products. */
export async function getCollectionProducts(collection: Collection): Promise<Product[]> {
  if (useMockData) {
    return collection.productSlugs
      .map((slug) => mockProducts.find((product) => product.slug === slug))
      .filter((product): product is Product => Boolean(product));
  }
  return api.get<Product[]>(`/collections/${encodeURIComponent(collection.slug)}/products`, {
    next: { revalidate: EDITORIAL_REVALIDATE },
  });
}

export async function getBrandProfiles(): Promise<Brand[]> {
  if (useMockData) {
    // Counts come from the catalogue so the directory never claims a number
    // the listing cannot show.
    return mockBrands.map((brand) => ({
      ...brand,
      productCount: mockProducts.filter((product) => product.brandSlug === brand.slug).length,
    }));
  }
  return api.get<Brand[]>('/brands', { next: { revalidate: EDITORIAL_REVALIDATE } });
}

export async function getBrandProfile(slug: string): Promise<Brand | null> {
  const brands = await getBrandProfiles();
  return brands.find((brand) => brand.slug === slug) ?? null;
}

export async function getArticles(category?: string): Promise<Article[]> {
  const all = useMockData
    ? mockArticles
    : await api.get<Article[]>('/articles', {
        next: { revalidate: EDITORIAL_REVALIDATE, tags: ['articles'] },
      });

  const sorted = [...all].sort(
    (a, b) => new Date(b.publishedAt).getTime() - new Date(a.publishedAt).getTime(),
  );
  return category ? sorted.filter((article) => article.category === category) : sorted;
}

/**
 * The categories the magazine's articles are actually filed under.
 *
 * The page used a hard-coded list, so a category the panel introduced never
 * appeared as a tab and one whose last article was unpublished stayed on the
 * page leading to an empty list. Derived in newest-first order, which is the
 * order the articles are already sorted in.
 */
export async function getArticleCategories(): Promise<string[]> {
  const all = await getArticles();
  return [...new Set(all.map((article) => article.category).filter(Boolean))];
}

export async function getArticle(slug: string): Promise<Article | null> {
  if (useMockData) return mockArticles.find((article) => article.slug === slug) ?? null;
  return api
    .get<Article>(`/articles/${encodeURIComponent(slug)}`, {
      next: { revalidate: EDITORIAL_REVALIDATE, tags: ['articles'] },
    })
    .catch(() => null);
}

export async function getRelatedArticles(slug: string, limit = 3): Promise<Article[]> {
  const all = await getArticles();
  return all.filter((article) => article.slug !== slug).slice(0, limit);
}
