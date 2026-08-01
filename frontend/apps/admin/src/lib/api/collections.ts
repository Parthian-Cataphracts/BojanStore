import { mockCollections } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminCollectionDto, Paged } from './types';

export interface ListCollectionsQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

function fromMock(item: (typeof mockCollections)[number]): AdminCollectionDto {
  return {
    id: item.id,
    title: item.name,
    slug: item.slug,
    summary: null,
    cover: null,
    editorialNote: null,
    featured: item.featured,
    productCount: item.productCount,
    status: 'published',
  };
}

export async function getCollections(query: ListCollectionsQuery = {}): Promise<Paged<AdminCollectionDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const q = (query.q ?? '').trim();
    const matched = mockCollections.filter((collection) => !q || collection.name.includes(q)).map(fromMock);
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<AdminCollectionDto>>('/collections', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

export async function getCollection(id: string): Promise<AdminCollectionDto | null> {
  if (useMockData) {
    const item = mockCollections.find((collection) => collection.id === id || collection.slug === id);
    return item ? fromMock(item) : null;
  }

  try {
    return await api.get<AdminCollectionDto>(`/collections/${id}`, { auth: true });
  } catch {
    return null;
  }
}
