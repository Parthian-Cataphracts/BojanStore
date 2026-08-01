import type { ProductQuery } from './api/types';

export type SearchParams = Record<string, string | string[] | undefined>;

export function first(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

/**
 * Translate URL search params into the API's query shape.
 * Shared by the product list, category and search screens so all three read
 * filters identically.
 */
export function toProductQuery(params: SearchParams): ProductQuery {
  const minPrice = first(params.minPrice);
  const maxPrice = first(params.maxPrice);
  const page = first(params.page);
  const category = first(params.category);
  const brand = first(params.brand);
  const search = first(params.q);
  const sort = first(params.sort);

  return {
    ...(category ? { category } : null),
    ...(brand ? { brand } : null),
    ...(search ? { search } : null),
    ...(minPrice ? { minPrice: Number(minPrice) } : null),
    ...(maxPrice ? { maxPrice: Number(maxPrice) } : null),
    ...(first(params.inStock) === 'true' ? { inStockOnly: true } : null),
    ...(sort ? { sort: sort as ProductQuery['sort'] } : null),
    ...(page ? { page: Number(page) } : null),
  };
}
