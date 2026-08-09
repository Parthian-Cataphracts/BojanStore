'use client';

import { useEffect, useRef, useState } from 'react';
import { Button, QuantityStepper } from '@bojan/ui';
import type { Product, ProductSku } from '@/lib/api/types';
import { useCart } from '@/lib/cart/store';
import { StickyActionBar } from '@/components/layout/StickyActionBar';

/**
 * Quantity + add-to-cart. Sticky above the bottom nav on mobile (screen 06),
 * inline in the details column on desktop.
 */
export function AddToCartBar({ product, sku }: { product: Product; sku?: ProductSku }) {
  const { addItem } = useCart();
  const [quantity, setQuantity] = useState(1);
  const [added, setAdded] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  // A variant with no matching SKU (an incomplete catalogue entry, or mock
  // mode where SKUs never load) falls back to the product's own stock — the
  // same number the shop sells from when there is no variant to pick.
  const stock = sku?.stock ?? product.stock;
  const soldOut = stock === 0;

  useEffect(() => {
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, []);

  function addToCart() {
    addItem(product, quantity, sku);

    // The design has no toast and no cart badge, so the button itself
    // acknowledges the click and then returns to its resting label.
    setAdded(true);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => setAdded(false), 2000);
  }

  return (
    <StickyActionBar className="items-center">
      <QuantityStepper
        value={quantity}
        onChange={setQuantity}
        max={Math.max(1, stock)}
        disabled={soldOut}
      />

      <Button
        size="lg"
        fullWidth
        disabled={soldOut}
        icon={added ? 'check' : 'shopping_cart'}
        onClick={addToCart}
      >
        {soldOut ? 'ناموجود' : added ? 'به سبد خرید اضافه شد' : 'افزودن به سبد خرید'}
      </Button>
    </StickyActionBar>
  );
}
