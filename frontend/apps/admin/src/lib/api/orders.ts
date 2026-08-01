import { mockAdminOrders } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminOrderDto, Paged } from './types';

export interface ListOrdersQuery {
  q?: string;
  status?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export async function getOrders(query: ListOrdersQuery = {}): Promise<Paged<AdminOrderDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const matched = mockAdminOrders.filter((order) => {
      const matchesStatus = !query.status || order.status === query.status;
      const q = (query.q ?? '').trim();
      const matchesQuery = !q || order.number.includes(q) || order.customer.includes(q);
      return matchesStatus && matchesQuery;
    });
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<AdminOrderDto>>('/orders', {
    query: { q: query.q, status: query.status, from: query.from, to: query.to, page, pageSize },
    auth: true,
  });
}

export async function getOrder(id: string): Promise<AdminOrderDto | null> {
  if (useMockData) {
    return mockAdminOrders.find((order) => order.id === id) ?? null;
  }

  try {
    return await api.get<AdminOrderDto>(`/orders/${id}`, { auth: true });
  } catch {
    return null;
  }
}
