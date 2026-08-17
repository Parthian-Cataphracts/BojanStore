import { matchesPersian } from '@bojan/ui';
import { mockCategories } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminCategoryDto, Paged } from './types';

export interface ListCategoriesQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

function fromMock(item: (typeof mockCategories)[number]): AdminCategoryDto {
  const parent = mockCategories.find((candidate) => candidate.name === item.parent) ?? null;
  return {
    id: item.id,
    name: item.name,
    slug: item.slug,
    icon: '',
    parentId: parent?.id ?? null,
    parentName: item.parent ?? null,
    productCount: item.productCount,
    status: 'published',
  };
}

export async function getCategories(
  query: ListCategoriesQuery = {},
): Promise<Paged<AdminCategoryDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const q = (query.q ?? '').trim();
    const matched = mockCategories
      .filter((category) => !q || matchesPersian(category.name, q))
      .map(fromMock);
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<AdminCategoryDto>>('/categories', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

export async function getCategory(id: string): Promise<AdminCategoryDto | null> {
  if (useMockData) {
    const item = mockCategories.find((category) => category.id === id || category.slug === id);
    return item ? fromMock(item) : null;
  }

  try {
    return await api.get<AdminCategoryDto>(`/categories/${id}`, { auth: true });
  } catch {
    return null;
  }
}
