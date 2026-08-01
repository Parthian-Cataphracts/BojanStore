import { mockBrands } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminBrandDto, Paged } from './types';

export interface ListBrandsQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

function fromMock(item: (typeof mockBrands)[number]): AdminBrandDto {
  return {
    id: item.id,
    name: item.name,
    slug: item.slug,
    tagline: null,
    description: null,
    logo: null,
    cover: null,
    featured: false,
    productCount: item.productCount,
    status: 'published',
  };
}

export async function getBrands(query: ListBrandsQuery = {}): Promise<Paged<AdminBrandDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const q = (query.q ?? '').trim();
    const matched = mockBrands.filter((brand) => !q || brand.name.includes(q)).map(fromMock);
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<AdminBrandDto>>('/brands', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

export async function getBrand(id: string): Promise<AdminBrandDto | null> {
  if (useMockData) {
    const item = mockBrands.find((brand) => brand.id === id || brand.slug === id);
    return item ? fromMock(item) : null;
  }

  try {
    return await api.get<AdminBrandDto>(`/brands/${id}`, { auth: true });
  } catch {
    return null;
  }
}
