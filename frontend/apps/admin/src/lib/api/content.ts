import { matchesPersian } from '@bojan/ui';
import { mockContent } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminArticleDto, AdminReviewDto, ContentEntryDto, Paged } from './types';

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
      const matchesQuery = !q || matchesPersian(entry.title, q);
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

/**
 * The review moderation queue.
 *
 * Reviews had a moderation state from the start and nothing in the panel could
 * change it, so every review a customer wrote sat at «در انتظار» and no product
 * page ever showed one. This is the screen that was missing.
 *
 * `status` accepts the three moderation states plus `featured`, which is not a
 * state but the subset of published reviews an operator has put on the home
 * page — a tab rather than a filter the API had to grow a second parameter for.
 *
 * No mock branch: there are no review fixtures on this side, and inventing them
 * would show an operator a queue of customers who do not exist.
 */
export interface ListReviewsQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getAdminReviews(
  query: ListReviewsQuery = {},
): Promise<Paged<AdminReviewDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) return { items: [], total: 0, page, pageSize };

  return api.get<Paged<AdminReviewDto>>('/reviews', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

/**
 * How many reviews sit in each state, for the tab counts.
 *
 * Zeroes rather than a throw when the call fails: the counts are a decoration
 * on a queue that renders fine without them, and failing the whole screen
 * because a badge could not be filled in would be the wrong trade.
 */
export async function getReviewCounts(): Promise<Record<string, number>> {
  if (useMockData) return {};

  try {
    return await api.get<Record<string, number>>('/reviews/counts', { auth: true });
  } catch {
    return {};
  }
}
