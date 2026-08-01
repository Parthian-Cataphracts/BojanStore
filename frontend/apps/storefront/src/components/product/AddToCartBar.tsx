'use client';

import { useEffect, useRef, useState } from 'react';
import { Button, QuantityStepper } from '@bojan/ui';
import type { Product } from '@/lib/api/types';
import { useCart } from '@/lib/cart/store';

/**
 * Quantity + add-to-cart. Sticky above the bottom nav on mobile (screen 06),
 * inline in the details column on desktop.
 */
export function AddToCartBar({ product }: { product: Product }) {
  const { addItem } = useCart();
  const [quantity, setQuantity] = useState(1);
  const [added, setAdded] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const soldOut = product.stock === 0;

  useEffect(() => {
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, []);

  function addToCart() {
    addItem(product, quantity);

    // The design has no toast and no cart badge, so the button itself
    // acknowledges the click and then returns to its resting label.
    setAdded(true);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => setAdded(false), 2000);
  }

  return (
    <div className="glass-nav fixed bottom-[72px] inset-x-0 z-40 flex items-center gap-md border-t border-outline-variant/40 px-margin-mobile py-md md:static md:border-0 md:bg-transparent md:p-0 md:backdrop-blur-none">
      <QuantityStepper
        value={quantity}
        onChange={setQuantity}
        max={Math.max(1, product.stock)}
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
    </div>
  );
}
