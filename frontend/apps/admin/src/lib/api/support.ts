import { matchesPersian } from '@bojan/ui';
import { cannedReplies, mockSupportThreads } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { CannedReplyDto, Paged, SupportThreadDetailDto, SupportThreadDto } from './types';

export interface ListSupportThreadsQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getSupportThreads(
  query: ListSupportThreadsQuery = {},
): Promise<Paged<SupportThreadDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const q = (query.q ?? '').trim();
    const matched = mockSupportThreads.filter((thread) => {
      const matchesStatus = !query.status || thread.status === query.status;
      const matchesQuery =
        !q || matchesPersian(thread.subject, q) || matchesPersian(thread.customer, q);
      return matchesStatus && matchesQuery;
    });
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<SupportThreadDto>>('/support/threads', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

export async function getSupportThread(id: string): Promise<SupportThreadDetailDto | null> {
  if (useMockData) {
    const thread = mockSupportThreads.find((t) => t.id === id);
    if (!thread) return null;
    return {
      id: thread.id,
      subject: thread.subject,
      customer: thread.customer,
      customerPhone: '0912-000-0000',
      customerEmail: null,
      status: thread.status,
      priority: thread.priority,
      updatedAt: thread.updatedAt,
      messages: [],
    };
  }

  try {
    return await api.get<SupportThreadDetailDto>(`/support/threads/${id}`, { auth: true });
  } catch {
    return null;
  }
}

export async function getCannedReplies(): Promise<CannedReplyDto[]> {
  if (useMockData) {
    return cannedReplies.map((reply) => ({ ...reply, updatedAt: new Date().toISOString() }));
  }

  return api.get<CannedReplyDto[]>('/support/canned-replies', { auth: true });
}
