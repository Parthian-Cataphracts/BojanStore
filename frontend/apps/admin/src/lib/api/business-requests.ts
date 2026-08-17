import { matchesPersian } from '@bojan/ui';
import { mockAdminProducts, mockB2BAdminRequests } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminBusinessRequestDto, AdminQuotableProductDto, Paged } from './types';

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
      const matchesQuery =
        !q || matchesPersian(request.organization, q) || matchesPersian(request.code, q);
      return matchesStatus && matchesQuery;
    });
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<AdminBusinessRequestDto>>('/business-requests', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

/**
 * The published catalogue with each product's volume ladder, for composing a
 * pro-forma.
 *
 * Empty on a failed read rather than a thrown page: the rest of the request
 * detail screen — the organisation, the contact, the notes, the assignment —
 * still works without it, and losing all of that because the picker could not
 * load is a worse answer than a picker that says it is empty.
 */
export async function getQuotableProducts(): Promise<AdminQuotableProductDto[]> {
  if (useMockData) {
    // Published only, like the API: a draft has no price the shop has committed
    // to and an archived product is not for sale, so quoting either promises an
    // organisation something the storefront will not honour.
    return mockAdminProducts
      .filter((product) => product.status === 'published')
      .map((product) => ({
        id: product.id,
        title: product.title,
        sku: product.sku,
        price: product.price,
        tiers: [],
      }));
  }

  return api
    .get<AdminQuotableProductDto[]>('/business-requests/products', { auth: true })
    .catch(() => []);
}
