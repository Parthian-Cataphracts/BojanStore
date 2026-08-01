'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { Suspense } from 'react';
import { Card, Icon, cn } from '@bojan/ui';

const ranges = [
  { value: '7d', label: '۷ روز اخیر' },
  { value: '30d', label: '۳۰ روز اخیر' },
  { value: '90d', label: '۳ ماه اخیر' },
  { value: 'year', label: 'سال جاری' },
];

function RangeChips() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const active = searchParams.get('range') ?? '30d';

  function pick(value: string) {
    const params = new URLSearchParams(searchParams.toString());
    params.set('range', value);
    router.push(`?${params.toString()}`, { scroll: false });
  }

  return (
    <Card className="flex flex-wrap items-center gap-sm p-md">
      <span className="flex items-center gap-xs text-caption text-on-surface-variant">
        <Icon name="date_range" size={18} />
        بازه زمانی:
      </span>

      {ranges.map((range) => (
        <button
          key={range.value}
          type="button"
          onClick={() => pick(range.value)}
          className={cn(
            'rounded-full px-md py-xs text-caption font-medium transition-colors',
            active === range.value
              ? 'bg-primary-fixed text-on-primary-fixed'
              : 'border border-outline-variant bg-surface-container text-on-surface hover:bg-surface-variant',
          )}
        >
          {range.label}
        </button>
      ))}
    </Card>
  );
}

/**
 * Date-range chips shared by the report screens (133-139).
 *
 * The Suspense boundary is required: `useSearchParams` opts the subtree out of
 * static prerendering, and without it the report pages fail to export.
 */
export function ReportRangePicker() {
  return (
    <Suspense fallback={<Card className="h-14 animate-pulse p-md" />}>
      <RangeChips />
    </Suspense>
  );
}
