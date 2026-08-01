'use client';

import { useEffect } from 'react';
import { useBrowsing } from '@/lib/browsing/store';

/**
 * Records a search term, for screen 90.
 *
 * Only terms that returned something are kept: a typo that found nothing is
 * not a search worth offering back to the shopper.
 */
export function RecordSearch({ term, hits }: { term: string; hits: number }) {
  const { recordSearch } = useBrowsing();

  useEffect(() => {
    if (term && hits > 0) recordSearch(term);
  }, [term, hits, recordSearch]);

  return null;
}
