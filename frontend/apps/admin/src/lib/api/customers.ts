import { mockAdminCustomers } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminAccountDto, AdminCustomerDto, Paged } from './types';

export interface ListCustomersQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getCustomers(query: ListCustomersQuery = {}): Promise<Paged<AdminCustomerDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const matched = mockAdminCustomers.filter((customer) => {
      const matchesStatus = !query.status || customer.status === query.status;
      const q = (query.q ?? '').trim();
      const matchesQuery = !q || customer.name.includes(q) || customer.phone.includes(q);
      return matchesStatus && matchesQuery;
    });
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<AdminCustomerDto>>('/customers', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

export async function getCustomer(id: string): Promise<AdminCustomerDto | null> {
  if (useMockData) {
    return mockAdminCustomers.find((customer) => customer.id === id) ?? null;
  }

  try {
    return await api.get<AdminCustomerDto>(`/customers/${id}`, { auth: true });
  } catch {
    return null;
  }
}


/**
 * Every account the shop has — shoppers and operators in one list.
 *
 * No mock branch that merges fixtures: the operator fixtures and the customer
 * fixtures were written for two different screens, and a list stitched from
 * them would show a shape the API does not return.
 */
export interface ListAccountsQuery {
  q?: string;
  role?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getAccounts(query: ListAccountsQuery = {}): Promise<Paged<AdminAccountDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) return { items: [], total: 0, page, pageSize };

  return api.get<Paged<AdminAccountDto>>('/accounts', {
    query: { q: query.q, role: query.role, status: query.status, page, pageSize },
    auth: true,
  });
}
