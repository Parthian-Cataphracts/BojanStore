import { mockContent } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { ContentEntryDto, Paged } from './types';

export interface ListContentQuery {
  q?: string;
  status?: string;
  kind?: string;
  page?: number;
  pageSize?: number;
}

export async function getContent(query: ListContentQuery = {}): Promise<Paged<ContentEntryDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const q = (query.q ?? '').trim();
    const matched = mockContent.filter((entry) => {
      const matchesStatus = !query.status || entry.status === query.status;
      const matchesKind = !query.kind || entry.type === query.kind;
      const matchesQuery = !q || entry.title.includes(q);
      return matchesStatus && matchesKind && matchesQuery;
    });
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<ContentEntryDto>>('/content', {
    query: { q: query.q, status: query.status, kind: query.kind, page, pageSize },
    auth: true,
  });
}

export async function getContentEntry(id: string): Promise<ContentEntryDto | null> {
  if (useMockData) {
    return mockContent.find((entry) => entry.id === id) ?? null;
  }

  try {
    return await api.get<ContentEntryDto>(`/content/${id}`, { auth: true });
  } catch {
    return null;
  }
}
