import type { ReactNode } from 'react';
import { Card, EmptyState, cn } from '@bojan/ui';
import { RowCheckbox, SelectAllCheckbox } from './TableSelection';
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
   * Tick-boxes down the leading edge, for a screen that acts on several rows
   * at once.
   *
   * What is ticked lives in `TableSelectionProvider`, which has to wrap this
   * table and whatever bar acts on the selection — see `TableSelection`. The
   * two labels are here because a bare checkbox has no accessible name, and
   * "checkbox" repeated twenty-one times is what a screen reader would
   * otherwise announce.
   */
  selectable?: {
    /** Names one row's box — the product's title, the order's number. */
    rowLabel: (row: T) => string;
    /** Names the header's box, and says out loud that it means this page. */
    allLabel: string;
  };
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
  selectable,
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
            {/*
              `text-table-header` and `text-table-cell` are in the type scale for
              this table and were not being used by it: both rows inherited
              16px/1.8 body copy, so a header read at the same weight and size as
              the data under it and twelve rows filled a screen. The scale's own
              14px puts the header a step above its column and gives the table
              back about a third of its height without touching the padding.
            */}
            <thead className="bg-surface-container-low text-table-header text-on-surface-variant">
              <tr>
                {selectable && (
                  <th scope="col" className="px-lg py-sm w-px">
                    <SelectAllCheckbox label={selectable.allLabel} />
                  </th>
                )}
                {columns.map((column) => (
                  <th
                    key={column.key}
                    scope="col"
                    style={column.width ? { width: column.width } : undefined}
                    className={cn(
                      'px-lg py-sm',
                      column.align === 'end' ? 'text-end' : 'text-start',
                    )}
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
                  className="border-outline-variant/30 hover:bg-surface-container-low border-t transition-colors"
                >
                  {selectable && (
                    <td className="px-lg py-md w-px">
                      <RowCheckbox id={rowKey(row)} label={selectable.rowLabel(row)} />
                    </td>
                  )}
                  {columns.map((column) => (
                    <td
                      key={column.key}
                      className={cn(
                        'px-lg py-md text-table-cell',
                        // An end-aligned column is a number in every table in
                        // this panel — a price, a count, a percentage — and
                        // digits that do not share a width make a column of them
                        // impossible to compare down the page.
                        column.align === 'end' ? 'text-end tabular-nums' : 'text-start',
                      )}
                    >
                      {column.cell(row)}
                    </td>
                  ))}
                  {actions && <td className="px-lg py-md text-table-cell">{actions(row)}</td>}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      {/* Mobile: cards */}
      <div className="gap-md flex flex-col md:hidden">
        {rows.map((row) => (
          <Card key={rowKey(row)} className="gap-sm p-md flex flex-col">
            {/* The box sits beside the line that names the row, because on a
                phone there is no leading column for it to head. */}
            <div className="gap-sm flex items-start">
              {selectable && (
                <span className="pt-px">
                  <RowCheckbox id={rowKey(row)} label={selectable.rowLabel(row)} />
                </span>
              )}
              {primary && (
                <div className="text-body-md text-primary flex-1 font-medium">
                  {primary.cell(row)}
                </div>
              )}
            </div>

            <dl className="gap-xs flex flex-col">
              {rest.map((column) => (
                <div key={column.key} className="gap-md flex items-center justify-between">
                  <dt className="text-caption text-on-surface-variant">{column.header}</dt>
                  <dd className="text-caption text-on-surface">{column.cell(row)}</dd>
                </div>
              ))}
            </dl>

            {actions && (
              <div className="gap-sm border-paper-border pt-sm flex flex-wrap border-t">
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
