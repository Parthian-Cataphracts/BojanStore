'use client';

import { createContext, useContext, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';

/**
 * The tick-boxes on a list screen, and what is ticked.
 *
 * State kept here rather than in `DataTable` because the table is a server
 * component: its `columns` are functions, so it cannot become a client one
 * without every screen that uses it rewriting how a cell is declared. What the
 * table renders instead are the two leaf controls below, which are client
 * components and find this context at runtime — a server component rendered as
 * `children` of a provider is still inside it on the page.
 *
 * The selection is the current page only. A shop with four hundred products
 * shows twenty at a time, and a control that said «همه» while meaning «همه‌ی
 * این صفحه» would be a control that deletes three hundred and eighty things
 * nobody looked at.
 */

interface TableSelectionValue {
  /** Every row on the page, in the order the table renders them. */
  ids: readonly string[];
  picked: ReadonlySet<string>;
  toggle: (id: string) => void;
  /** Ticks everything on the page, or clears it when everything already is. */
  toggleAll: () => void;
  clear: () => void;
}

const TableSelectionContext = createContext<TableSelectionValue | null>(null);

export function useTableSelection(): TableSelectionValue {
  const value = useContext(TableSelectionContext);
  if (!value) {
    throw new Error('useTableSelection must be used inside a <TableSelectionProvider>.');
  }
  return value;
}

export function TableSelectionProvider({
  ids,
  children,
}: {
  ids: readonly string[];
  children: ReactNode;
}) {
  const [ticked, setTicked] = useState<ReadonlySet<string>>(() => new Set());

  const available = useMemo(() => new Set(ids), [ids]);

  /*
    Filtered on the way out rather than pruned in an effect. The rows change
    under this component — a filter, the next page, or the refresh that follows
    a bulk action — and the ids it was holding are then ids of rows that are no
    longer on screen. Deriving what is picked from what is present means the
    next action can only ever reach rows the operator can see, with no window
    where an effect has not run yet.
  */
  const picked = useMemo(
    () => new Set([...ticked].filter((id) => available.has(id))),
    [ticked, available],
  );

  const value = useMemo<TableSelectionValue>(
    () => ({
      ids,
      picked,
      toggle: (id) =>
        setTicked((current) => {
          const next = new Set(current);
          if (!next.delete(id)) next.add(id);
          return next;
        }),
      toggleAll: () => setTicked(picked.size === ids.length ? new Set() : new Set(ids)),
      clear: () => setTicked(new Set()),
    }),
    [ids, picked],
  );

  return <TableSelectionContext.Provider value={value}>{children}</TableSelectionContext.Provider>;
}

/**
 * The box in the header row.
 *
 * Three states, not two: empty, every row, and the half-way house that HTML
 * spells `indeterminate` and only script can set. Without it a page with three
 * of twenty ticked shows an empty box, and clicking it looks like it should
 * tick everything — which it does, after first appearing to have done nothing.
 */
export function SelectAllCheckbox({ label }: { label: string }) {
  const { ids, picked, toggleAll } = useTableSelection();
  const ref = useRef<HTMLInputElement>(null);

  const all = ids.length > 0 && picked.size === ids.length;
  const some = picked.size > 0 && !all;

  useEffect(() => {
    if (ref.current) ref.current.indeterminate = some;
  }, [some]);

  return (
    <input
      ref={ref}
      type="checkbox"
      aria-label={label}
      checked={all}
      disabled={ids.length === 0}
      onChange={toggleAll}
      className="border-outline-variant text-primary focus:ring-primary/30 h-5 w-5 shrink-0 rounded focus:ring-2"
    />
  );
}

/** The box on one row. Labelled by what the row is, since nothing beside it says so. */
export function RowCheckbox({ id, label }: { id: string; label: string }) {
  const { picked, toggle } = useTableSelection();

  return (
    <input
      type="checkbox"
      aria-label={label}
      checked={picked.has(id)}
      onChange={() => toggle(id)}
      className="border-outline-variant text-primary focus:ring-primary/30 h-5 w-5 shrink-0 rounded focus:ring-2"
    />
  );
}
