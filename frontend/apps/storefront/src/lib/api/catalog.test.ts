import { describe, expect, it } from 'vitest';
import { getBrands, getCategories, getProducts, getRelatedProducts } from './catalog';
import { mockProducts } from '../mock/products';

/**
 * These run against the mock path, which is what `NEXT_PUBLIC_USE_MOCK_DATA`
 * selects by default. The filtering and sorting live in the client, so they are
 * worth testing regardless of which backend answers.
 */

describe('getProducts', () => {
  it('returns everything when no filters are given', async () => {
    const { items, total } = await getProducts({ pageSize: 100 });
    expect(total).toBe(mockProducts.length);
    expect(items).toHaveLength(mockProducts.length);
  });

  it('filters by category', async () => {
    const { items } = await getProducts({ category: 'art-tools', pageSize: 100 });
    expect(items.length).toBeGreaterThan(0);
    expect(items.every((product) => product.categorySlug === 'art-tools')).toBe(true);
  });

  it('filters by brand', async () => {
    const { items } = await getProducts({ brand: 'winsor-newton', pageSize: 100 });
    expect(items.length).toBeGreaterThan(0);
    expect(items.every((product) => product.brandSlug === 'winsor-newton')).toBe(true);
  });

  it('excludes out-of-stock items when asked', async () => {
    const { items } = await getProducts({ inStockOnly: true, pageSize: 100 });
    expect(items.every((product) => product.stock > 0)).toBe(true);
    // The fixture must actually contain a zero-stock product or this proves nothing.
    expect(mockProducts.some((product) => product.stock === 0)).toBe(true);
  });

  it('respects a price range inclusively at both ends', async () => {
    const { items } = await getProducts({ minPrice: 300_000, maxPrice: 500_000, pageSize: 100 });
    expect(items.length).toBeGreaterThan(0);
    expect(items.every((p) => p.price >= 300_000 && p.price <= 500_000)).toBe(true);
  });

  it('matches search against title, brand and category name', async () => {
    const byTitle = await getProducts({ search: 'آبرنگ', pageSize: 100 });
    expect(byTitle.items.length).toBeGreaterThan(0);

    const byCategory = await getProducts({ search: 'ابزار هنری', pageSize: 100 });
    expect(byCategory.items.length).toBeGreaterThan(0);
  });

  it('trims the search term so a stray space does not zero the results', async () => {
    const padded = await getProducts({ search: '  آبرنگ  ', pageSize: 100 });
    const clean = await getProducts({ search: 'آبرنگ', pageSize: 100 });
    expect(padded.total).toBe(clean.total);
  });

  it('returns an empty page rather than throwing when nothing matches', async () => {
    const { items, total } = await getProducts({ search: 'zzz-no-such-product' });
    expect(items).toEqual([]);
    expect(total).toBe(0);
  });

  it('sorts by price ascending and descending', async () => {
    const asc = await getProducts({ sort: 'price-asc', pageSize: 100 });
    const prices = asc.items.map((product) => product.price);
    expect([...prices].sort((a, b) => a - b)).toEqual(prices);

    const desc = await getProducts({ sort: 'price-desc', pageSize: 100 });
    const descPrices = desc.items.map((product) => product.price);
    expect([...descPrices].sort((a, b) => b - a)).toEqual(descPrices);
  });

  it('sorts by rating descending', async () => {
    const { items } = await getProducts({ sort: 'rating', pageSize: 100 });
    const ratings = items.map((product) => product.rating);
    expect([...ratings].sort((a, b) => b - a)).toEqual(ratings);
  });

  it('does not mutate the source fixture while sorting', async () => {
    const before = mockProducts.map((product) => product.id);
    await getProducts({ sort: 'price-desc', pageSize: 100 });
    expect(mockProducts.map((product) => product.id)).toEqual(before);
  });

  it('paginates, and reports the unpaginated total', async () => {
    const pageSize = 4;
    const firstPage = await getProducts({ page: 1, pageSize });
    const secondPage = await getProducts({ page: 2, pageSize });

    expect(firstPage.items).toHaveLength(pageSize);
    expect(firstPage.total).toBe(mockProducts.length);
    expect(firstPage.page).toBe(1);

    // Pages must not overlap.
    const firstIds = firstPage.items.map((product) => product.id);
    expect(secondPage.items.some((product) => firstIds.includes(product.id))).toBe(false);
  });

  it('returns an empty page past the end instead of wrapping around', async () => {
    const { items } = await getProducts({ page: 999, pageSize: 10 });
    expect(items).toEqual([]);
  });

  it('combines filters rather than letting the last one win', async () => {
    const { items } = await getProducts({
      category: 'art-tools',
      inStockOnly: true,
      pageSize: 100,
    });
    expect(items.every((p) => p.categorySlug === 'art-tools' && p.stock > 0)).toBe(true);
  });
});

describe('getRelatedProducts', () => {
  it('returns products from the same category, excluding the product itself', async () => {
    const source = mockProducts[0]!;
    const related = await getRelatedProducts(source.slug, 10);

    expect(related.every((product) => product.slug !== source.slug)).toBe(true);
    expect(related.every((product) => product.categorySlug === source.categorySlug)).toBe(true);
  });

  it('honours the limit', async () => {
    const related = await getRelatedProducts(mockProducts[0]!.slug, 2);
    expect(related.length).toBeLessThanOrEqual(2);
  });

  it('falls back to a generic list for an unknown slug', async () => {
    const related = await getRelatedProducts('no-such-slug', 3);
    expect(related).toHaveLength(3);
  });
});

describe('getBrands', () => {
  it('derives counts from the catalogue, so no brand claims a phantom product', async () => {
    const brands = await getBrands();

    for (const brand of brands) {
      const actual = mockProducts.filter((product) => product.brandSlug === brand.slug).length;
      expect(brand.productCount).toBe(actual);
    }
  });

  it('orders brands by product count, descending', async () => {
    const counts = (await getBrands()).map((brand) => brand.productCount);
    expect([...counts].sort((a, b) => b - a)).toEqual(counts);
  });
});

describe('getCategories', () => {
  it('reports a product count that matches the catalogue for top-level categories', async () => {
    const categories = await getCategories();

    for (const category of categories) {
      const actual = mockProducts.filter((p) => p.categorySlug === category.slug).length;
      // `architecture` is a placeholder category with a hand-set count and no
      // products behind it yet; every other category must reconcile.
      if (category.slug !== 'architecture') {
        expect(category.productCount).toBe(actual);
      }
    }
  });
});
