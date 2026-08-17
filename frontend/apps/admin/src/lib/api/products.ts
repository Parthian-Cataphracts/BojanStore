import { matchesPersian } from '@bojan/ui';
import { mockAdminProducts } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type {
  AdminAttributeDto,
  ProductVolumeTierDto,
  AdminProductDto,
  AdminSkuDto,
  AdminVariantAxisDto,
  Paged,
} from './types';

export interface ListProductsQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getProducts(query: ListProductsQuery = {}): Promise<Paged<AdminProductDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const matched = mockAdminProducts.filter((product) => {
      const matchesStatus = !query.status || product.status === query.status;
      const q = (query.q ?? '').trim();
      const matchesQuery = !q || matchesPersian(product.title, q) || matchesPersian(product.sku, q);
      return matchesStatus && matchesQuery;
    });
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<AdminProductDto>>('/products', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

export async function getProduct(id: string): Promise<AdminProductDto | null> {
  if (useMockData) {
    return mockAdminProducts.find((product) => product.id === id) ?? null;
  }

  try {
    return await api.get<AdminProductDto>(`/products/${id}`, { auth: true });
  } catch {
    return null;
  }
}

/**
 * Screens 106-108 — the product's variants, SKUs and attributes.
 *
 * Each is its own request rather than a field on the product: they are three
 * separate screens, and the product form has no use for any of them. An empty
 * list is the answer for a product that has none, so a failure here is a
 * failure — not something to paper over with `[]`, which would render as "this
 * product has no variants" when the truth is that nobody could tell.
 */
export async function getProductVariants(id: string): Promise<AdminVariantAxisDto[]> {
  if (useMockData) return [];
  return api.get<AdminVariantAxisDto[]>(`/products/${id}/variants`, { auth: true });
}

export async function getProductSkus(id: string): Promise<AdminSkuDto[]> {
  if (useMockData) return [];
  return api.get<AdminSkuDto[]>(`/products/${id}/skus`, { auth: true });
}

export async function getProductAttributes(id: string): Promise<AdminAttributeDto[]> {
  if (useMockData) return [];
  return api.get<AdminAttributeDto[]>(`/products/${id}/attributes`, { auth: true });
}

/**
 * A product's B2B volume ladder.
 *
 * Empty rather than a fixture in mock mode, and empty on a read that fails: no
 * ladder means the product sells at its list price to organisations too, which
 * is the honest fallback — inventing rungs would quote money the shop never
 * agreed to.
 */
export async function getProductVolumeTiers(id: string): Promise<ProductVolumeTierDto[]> {
  if (useMockData) return [];
  return api
    .get<ProductVolumeTierDto[]>(`/products/${id}/volume-tiers`, { auth: true })
    .catch(() => []);
}
