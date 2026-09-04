'use client';

import { useMemo, useState } from 'react';
import { Icon, Price, toPersianDigits } from '@bojan/ui';
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

  /*
    The headline price and the stock line belong to whatever the shopper has
    picked, so they live inside this client boundary rather than in the server
    page above it.

    They used to be rendered from `product` alone and never moved: choosing the
    ۱۱٬۰۰۰ size left «۱۰٬۰۰۰ تومان» in place — the cheapest combination's price,
    which is what a listing card shows — and the stock line offered the
    product's whole count while the size in front of the shopper had four. Both
    read as the price and stock of the thing being bought, and neither was.
  */
  const price = sku?.price ?? product.price;
  const compareAt = sku ? (sku.compareAt ?? undefined) : product.compareAtPrice;
  const stock = sku?.stock ?? product.stock;

  return (
    <>
      <Price
        value={price}
        {...(compareAt !== undefined && compareAt > price ? { compareAt } : null)}
        size="lg"
        className="mt-sm"
      />

      <p
        className={`gap-xs text-caption flex items-center ${
          stock > 0 ? 'text-tertiary' : 'text-error'
        }`}
      >
        <Icon name={stock > 0 ? 'check_circle' : 'cancel'} size={16} />
        {stock > 0
          ? `موجود در انبار (${toPersianDigits(stock)} عدد)`
          : 'در حال حاضر ناموجود است'}
      </p>

      {variantAxes.length > 0 && (
        <VariantSelector axes={variantAxes} skus={skus} onChange={setCombination} />
      )}
      <AddToCartBar product={product} sku={sku} requiresSku={requiresSku} />
    </>
  );
}
