import { mockAdminProducts, mockStockMovements } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { InventoryRowDto, Paged, StockMovementDto } from './types';

/** No dedicated inventory fixture exists — the product list already carries stock. */
const LOW_STOCK_THRESHOLD = 10;

const mockInventoryRows: InventoryRowDto[] = mockAdminProducts.map((product) => ({
  id: product.id,
  sku: product.sku,
  title: product.title,
  category: product.category,
  stock: product.stock,
  lowStockThreshold: LOW_STOCK_THRESHOLD,
  updatedAt: product.updatedAt,
}));

export interface ListInventoryQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getInventory(query: ListInventoryQuery = {}): Promise<Paged<InventoryRowDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const q = (query.q ?? '').trim();
    const matched = mockInventoryRows.filter(
      (row) => !q || row.title.includes(q) || row.sku.includes(q),
    );
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<InventoryRowDto>>('/inventory', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

export interface ListStockMovementsQuery {
  kind?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export async function getStockMovements(
  query: ListStockMovementsQuery = {},
): Promise<Paged<StockMovementDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const matched = mockStockMovements.filter((movement) => !query.kind || movement.kind === query.kind);
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<StockMovementDto>>('/inventory/movements', {
    query: { kind: query.kind, from: query.from, to: query.to, page, pageSize },
    auth: true,
  });
}
