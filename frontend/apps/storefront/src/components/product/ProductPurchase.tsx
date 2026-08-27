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

  /*
    Whether a pick has to resolve to a combination before anything can be
    bought.

    Only when the product has both axes and combinations. Axes with no
    combinations at all is a different state — a half-filled form, or mock mode,
    where SKUs never load — and there the product's own price and stock are all
    there is, which is what the bar has always fallen back to.

    With both, an unresolved pick means the shopper is looking at a size the
    shop does not stock, and the bar must not quietly sell them the plain
    product instead.
  */
  const requiresSku = variantAxes.length > 0 && skus.length > 0;

  return (
    <>
      {variantAxes.length > 0 && (
        <VariantSelector axes={variantAxes} skus={skus} onChange={setCombination} />
      )}
      <AddToCartBar product={product} sku={sku} requiresSku={requiresSku} />
    </>
  );
}
