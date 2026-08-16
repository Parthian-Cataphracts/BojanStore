'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { Icon, JalaliDateInput } from '@bojan/ui';

/**
 * The «از تاریخ» / «تا تاریخ» pair on the audit screen.
 *
 * Its own component rather than two fields on the page, because the page is a
 * server component and a date picker is not: this is the client edge, and all
 * it does is write the chosen days into the URL. Everything below it — the
 * fetch, the filtering, the paging — stays on the server and reads the URL like
 * every other list in the panel does, so a filtered trail is still a link
 * somebody can send.
 *
 * `JalaliDateInput` rather than `<input type="date">` for the reason it exists:
 * a browser's own picker draws Gregorian months, and an operator looking for
 * what happened in «مرداد» does not want to be asked about August. It hands
 * back ISO either way, which is what the API takes.
 */
export function AuditDateRange({ from, to }: { from: string; to: string }) {
  const router = useRouter();
  const searchParams = useSearchParams();

  function apply(key: 'from' | 'to', value: string) {
    const params = new URLSearchParams(searchParams.toString());
    if (value) params.set(key, value);
    else params.delete(key);

    // Narrowing the window changes what the first page holds, so the operator
    // goes back to it — the same rule `FilterBar` follows for every other
    // filter in the panel.
    params.delete('page');

    const query = params.toString();
    router.push(query ? `?${query}` : '?', { scroll: false });
  }

  return (
    <div className="gap-md border-outline-variant/70 bg-surface-container-lowest p-md flex flex-col rounded-xl border sm:flex-row sm:items-end">
      <span className="gap-xs pb-md text-caption text-on-surface-variant flex items-center sm:pb-3">
        <Icon name="date_range" size={18} />
        بازه زمانی:
      </span>

      <JalaliDateInput
        label="از تاریخ"
        value={from}
        onChange={(value) => apply('from', value)}
        yearsBack={5}
        wrapperClassName="flex-1"
      />
      <JalaliDateInput
        label="تا تاریخ"
        value={to}
        onChange={(value) => apply('to', value)}
        yearsBack={5}
        wrapperClassName="flex-1"
      />
    </div>
  );
}
