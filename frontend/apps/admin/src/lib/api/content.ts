import { mockContent } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminArticleDto, ContentEntryDto, Paged } from './types';

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

/**
 * The magazine's own articles.
 *
 * A different endpoint from `getContent` above and a different table behind it.
 * `/content` holds static pages, banners and FAQ entries; `/articles` is what
 * the storefront's magazine reads, and until now the panel wrote articles into
 * the first one — so nothing published here ever reached the site.
 *
 * No mock branch: there are no article fixtures on this side, and inventing
 * them would put a magazine in the panel that the storefront does not have.
 */
export interface ListArticlesQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getAdminArticles(
  query: ListArticlesQuery = {},
): Promise<Paged<AdminArticleDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) return { items: [], total: 0, page, pageSize };

  return api.get<Paged<AdminArticleDto>>('/articles', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

export async function getAdminArticle(id: string): Promise<AdminArticleDto | null> {
  if (useMockData) return null;

  try {
    return await api.get<AdminArticleDto>(`/articles/${id}`, { auth: true });
  } catch {
    return null;
  }
}
