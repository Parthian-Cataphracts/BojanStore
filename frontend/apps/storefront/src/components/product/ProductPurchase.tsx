'use client';

import { useMemo, useState } from 'react';
import type { Product, ProductSku, ProductVariantAxis } from '@/lib/api/types';
import { AddToCartBar } from './AddToCartBar';
import { VariantSelector } from './VariantSelector';

/**
 * Screens 06 and 86 together: the variant pick has to reach the add-to-cart
 * bar, and a server component can't hold that state itself. This is the one
 * client boundary that does — the axes and SKUs still come from the server.
 */
export function ProductPurchase({
  product,
  variantAxes,
  skus,
}: {
  product: Product;
  variantAxes: ProductVariantAxis[];
  skus: ProductSku[];
}) {
  const [combination, setCombination] = useState('');

  // Absent for a product with no axes, or when the picked combination has no
  // matching SKU (an incomplete catalogue entry) — either way `AddToCartBar`
  // falls back to the product's own price and stock.
  const sku = useMemo(
    () => skus.find((candidate) => candidate.combination === combination),
    [skus, combination],
  );

  return (
    <>
      {variantAxes.length > 0 && <VariantSelector axes={variantAxes} onChange={setCombination} />}
      <AddToCartBar product={product} sku={sku} />
    </>
  );
}
