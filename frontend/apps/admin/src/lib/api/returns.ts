import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminReturnDto, Paged } from './types';

/**
 * The returns queue — `GET /admin/returns` and `GET /admin/returns/{id}`.
 *
 * No mock fixtures. Every other panel list has them because the screens were
 * drawn before the API existed; these were not, and inventing returns to look
 * at would mean an operator in mock mode could press "بازپرداخت" on a request
 * that does not exist. An empty queue is the honest answer when there is no
 * server to ask.
 */

export interface ListReturnsQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getReturns(query: ListReturnsQuery = {}): Promise<Paged<AdminReturnDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) return paginate([] as AdminReturnDto[], page, pageSize);

  return api.get<Paged<AdminReturnDto>>('/returns', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

export async function getReturn(id: string): Promise<AdminReturnDto | null> {
  if (useMockData) return null;

  try {
    return await api.get<AdminReturnDto>(`/returns/${id}`, { auth: true });
  } catch {
    return null;
  }
}
