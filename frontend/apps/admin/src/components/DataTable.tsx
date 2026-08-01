import type { ReactNode } from 'react';
import { Card, EmptyState, cn } from '@bojan/ui';
import { TablePagination } from './TablePagination';

export interface Column<T> {
  key: string;
  header: string;
  /** Cell renderer. Return a string for plain text. */
  cell: (row: T) => ReactNode;
  /** Hide below `md` — the mobile card shows it under the primary line. */
  secondary?: boolean;
  align?: 'start' | 'end';
  width?: string;
}

export interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string;
  /** Row-level actions rendered in a trailing column. */
  actions?: (row: T) => ReactNode;
  emptyTitle?: string;
  emptyDescription?: string;
  emptyIcon?: string;
  /**
   * Paging. Omit it and the table renders every row it is given — right for the
   * short reference lists, wrong for orders, products and customers.
   */
  pagination?: {
    page: number;
    pageSize: number;
    total: number;
    /** Current search params, so a page link keeps the active filters. */
    params: Record<string, string | string[] | undefined>;
    basePath: string;
  };
}

/**
 * Admin table.
 *
 * Renders a real `<table>` from `md` up and a card list below it — a
 * horizontally scrolling table is unusable on a phone, and the admin screens
 * are drawn as cards there.
 */
export function DataTable<T>({
  columns,
  rows,
  rowKey,
  actions,
  emptyTitle = 'موردی یافت نشد',
  emptyDescription = 'با فیلترهای فعلی رکوردی وجود ندارد.',
  emptyIcon = 'inbox',
  pagination,
}: DataTableProps<T>) {
  if (rows.length === 0) {
    return (
      <Card surface="plain" className="p-lg">
        <EmptyState icon={emptyIcon} title={emptyTitle} description={emptyDescription} />
      </Card>
    );
  }

  const [primary, ...rest] = columns;

  return (
    <>
      {/* Desktop: table */}
      <Card surface="plain" className="hidden overflow-hidden md:block">
        <div className="overflow-x-auto">
          <table className="w-full text-start">
            <thead className="bg-surface-container-low text-on-surface-variant">
              <tr>
                {columns.map((column) => (
                  <th
                    key={column.key}
                    scope="col"
                    style={column.width ? { width: column.width } : undefined}
                    className={cn('px-lg py-sm', column.align === 'end' ? 'text-end' : 'text-start')}
                  >
                    {column.header}
                  </th>
                ))}
                {actions && (
                  <th scope="col" className="px-lg py-sm text-start">
                    عملیات
                  </th>
                )}
              </tr>
            </thead>

            <tbody>
              {rows.map((row) => (
                <tr
                  key={rowKey(row)}
                  className="border-t border-outline-variant/30 transition-colors hover:bg-surface-container-low"
                >
                  {columns.map((column) => (
                    <td
                      key={column.key}
                      className={cn('px-lg py-md', column.align === 'end' ? 'text-end' : 'text-start')}
                    >
                      {column.cell(row)}
                    </td>
                  ))}
                  {actions && <td className="px-lg py-md">{actions(row)}</td>}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      {/* Mobile: cards */}
      <div className="flex flex-col gap-md md:hidden">
        {rows.map((row) => (
          <Card key={rowKey(row)} className="flex flex-col gap-sm p-md">
            {primary && <div className="text-body-md font-medium text-primary">{primary.cell(row)}</div>}

            <dl className="flex flex-col gap-xs">
              {rest.map((column) => (
                <div key={column.key} className="flex items-center justify-between gap-md">
                  <dt className="text-caption text-on-surface-variant">{column.header}</dt>
                  <dd className="text-caption text-on-surface">{column.cell(row)}</dd>
                </div>
              ))}
            </dl>

            {actions && (
              <div className="flex flex-wrap gap-sm border-t border-paper-border pt-sm">
                {actions(row)}
              </div>
            )}
          </Card>
        ))}
      </div>

      {pagination && <TablePagination {...pagination} />}
    </>
  );
}
