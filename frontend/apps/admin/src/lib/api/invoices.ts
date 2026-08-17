import { toLatinDigits, matchesPersian } from '@bojan/ui';
import { mockInvoices, mockInvoiceDocument } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { InvoiceDto, InvoiceSummaryDto, Paged } from './types';

export interface ListInvoicesQuery {
  q?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export async function getInvoices(
  query: ListInvoicesQuery = {},
): Promise<Paged<InvoiceSummaryDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const term = (query.q ?? '').trim();
    // The backend normalises the term the same way (`PersianDigits.ToLatin`),
    // so a Persian-typed number searches identically with the API up or down.
    const digits = toLatinDigits(term).replace(/\D/g, '');
    const matched = mockInvoices.filter(
      (invoice) =>
        !term ||
        (digits.length > 0 && invoice.invoiceNumber.includes(digits)) ||
        matchesPersian(invoice.orderNumber, term) ||
        matchesPersian(invoice.customer, term) ||
        matchesPersian(invoice.customerPhone, term),
    );
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<InvoiceSummaryDto>>('/invoices', {
    query: { q: query.q, from: query.from, to: query.to, page, pageSize },
    auth: true,
  });
}

/**
 * The invoice document for one order, or null when it has none.
 *
 * Null covers both "no such order" and "not delivered yet" — the API answers
 * 404 for either, because an order that has not been delivered has no invoice
 * to fetch rather than one being withheld.
 */
export async function getInvoice(orderId: string): Promise<InvoiceDto | null> {
  if (useMockData) {
    return mockInvoiceDocument(orderId);
  }

  try {
    return await api.get<InvoiceDto>(`/orders/${orderId}/invoice`, { auth: true });
  } catch {
    return null;
  }
}
