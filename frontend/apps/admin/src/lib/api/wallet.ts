import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { Paged } from './types';

/** A card-to-card top-up awaiting a decision. */
export interface AdminWalletTopUpDto {
  id: string;
  customerId: string;
  customerName: string;
  customerPhone: string;
  amount: number;
  method: 'gateway' | 'manual';
  status: 'pending' | 'approved' | 'rejected';
  trackingNumber?: string;
  paidOn?: string;
  receiptUrl?: string;
  customerNote?: string;
  createdAt: string;
}

export interface ListWalletTopUpsQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

/**
 * The review queue.
 *
 * No fixtures behind the mock branch, and deliberately so: an invented queue of
 * transfers to approve is the one screen where practising on fake data teaches
 * exactly the wrong reflex. With the fixtures on, it is simply empty.
 */
export async function getWalletTopUps(
  query: ListWalletTopUpsQuery = {},
): Promise<Paged<AdminWalletTopUpDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) return paginate<AdminWalletTopUpDto>([], page, pageSize);

  return api.get<Paged<AdminWalletTopUpDto>>('/wallet/topups', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}
