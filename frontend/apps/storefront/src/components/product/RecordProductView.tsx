'use client';

import { useEffect } from 'react';
import { useBrowsing } from '@/lib/browsing/store';
import type { Product } from '@/lib/api/types';

/**
 * Records that this product was opened, for screen 57.
 *
 * Renders nothing — the product page is server-rendered and static, and this is
 * the one thing on it that has to happen in the browser. Keyed on the product
 * id so navigating between products records each one.
 */
export function RecordProductView({ product }: { product: Product }) {
  const { recordView } = useBrowsing();

  useEffect(() => {
    recordView(product);
    // `product` is a new object on every render of the server component, so the
    // id is what actually identifies a change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [product.id, recordView]);

  return null;
}
