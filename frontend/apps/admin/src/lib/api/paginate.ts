import type { Paged } from './types';

/** Slices an in-memory fixture array into the `Paged<T>` shape the backend returns. */
export function paginate<T>(items: T[], page: number, pageSize: number): Paged<T> {
  const start = (page - 1) * pageSize;
  return { items: items.slice(start, start + pageSize), total: items.length, page, pageSize };
}

export const DEFAULT_PAGE_SIZE = 20;
