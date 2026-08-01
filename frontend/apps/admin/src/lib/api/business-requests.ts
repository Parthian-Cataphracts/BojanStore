import { mockB2BAdminRequests } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminBusinessRequestDto, Paged } from './types';

const mockBusinessRequests: AdminBusinessRequestDto[] = mockB2BAdminRequests.map((request) => ({
  id: request.id,
  code: request.code,
  title: request.organization,
  kind: 'quote',
  status: request.status,
  organization: request.organization,
  contact: request.contact,
  phone: request.phone,
  email: request.email,
  itemCount: request.itemCount,
  assigneeId: null,
  note: null,
  createdAt: request.createdAt,
}));

export interface ListBusinessRequestsQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getBusinessRequests(
  query: ListBusinessRequestsQuery = {},
): Promise<Paged<AdminBusinessRequestDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const q = (query.q ?? '').trim();
    const matched = mockBusinessRequests.filter((request) => {
      const matchesStatus = !query.status || request.status === query.status;
      const matchesQuery = !q || request.organization.includes(q) || request.code.includes(q);
      return matchesStatus && matchesQuery;
    });
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<AdminBusinessRequestDto>>('/business-requests', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}
