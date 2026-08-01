import { mockAdminProducts } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminProductDto, Paged } from './types';

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
      const matchesQuery = !q || product.title.includes(q) || product.sku.includes(q);
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
