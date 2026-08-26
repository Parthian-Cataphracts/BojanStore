'use client';

import { useEffect, useRef, useState } from 'react';
import { Button, Price, QuantityStepper } from '@bojan/ui';
import type { Product, ProductSku } from '@/lib/api/types';
import { useCart } from '@/lib/cart/store';
import { StickyActionBar } from '@/components/layout/StickyActionBar';

/**
 * Quantity + add-to-cart. Sticky above the bottom nav on mobile (screen 06),
 * inline in the details column on desktop.
 *
 * The two states carry different things, because they are read in different
 * places. Inline on desktop the bar sits directly under the price, so it shows
 * the stepper and the button. Floating over a phone it is usually the only
 * part of the page on screen — the shopper has scrolled down to the reviews —
 * so it carries the price itself and drops the stepper: a number nobody can
 * see is not one they can decide with, and the quantity is a tap away in the
 * basket. It is the same bargain the cart's bar makes, and the same one the
 * large stores make on this screen.
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
  // A picked variant prices itself; the product's own price is the fallback,
  // and the list price beside it belongs to the product either way.
  const price = sku?.price ?? product.price;
  const compareAt = product.compareAtPrice;

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
    /*
      `py-sm` while floating: the same height as the cart's bar, so the two read
      as one piece of furniture rather than two.

      `flex-row-reverse` puts the price on the left of the bar and the button on
      the right — where the shops shoppers already use put them. The page is
      RTL, so the plain order would have mirrored that: price right, button
      left. The DOM keeps information before action; only the painting flips.
      Inline from `md` the bar is an ordinary RTL row again.
    */
    <StickyActionBar className="flex-row-reverse items-center justify-between py-sm md:flex-row md:py-0">
      {/*
        Floating only: inline, the real price is two lines above this.

        Held to a measure so it stacks — badge and list price on one line, the
        price itself under them — instead of running 200px along the bar and
        pushing the button onto a row of its own.
      */}
      <Price
        value={price}
        {...(compareAt !== undefined && compareAt > price ? { compareAt } : null)}
        // `justify-end` is the *left* in an RTL row: the block sits at the
        // left edge of the bar, so its two lines flush left with each other
        // rather than hanging off the right of a 136px box.
        className="max-w-[8.5rem] justify-end md:hidden"
      />

      <QuantityStepper
        value={quantity}
        onChange={setQuantity}
        max={Math.max(1, stock)}
        disabled={soldOut}
        className="hidden md:flex"
      />

      {/*
        Inline, a basis wide enough for the longest of the three labels — «به
        سبد خرید اضافه شد» — rather than `w-full`'s «all of it», so the label
        stays on one line, which a button with a fixed height has no room to do
        otherwise. Floating, it takes whatever the price leaves and no fixed
        basis at all: 13rem beside a price with a discount badge is more than a
        375px phone has, and the bar answered by wrapping the button onto a
        second row and doubling its own height.
      */}
      <Button
        size="lg"
        fullWidth
        // A definite basis, not `auto`: `fullWidth` puts `w-full` on the
        // button, and a flex item with `basis-auto` takes that 100% as its
        // basis and wraps onto its own row every time.
        className="flex-1 basis-[9rem] whitespace-nowrap md:basis-[13rem]"
        disabled={soldOut}
        icon={added ? 'check' : 'shopping_cart'}
        onClick={addToCart}
      >
        {soldOut ? (
          'ناموجود'
        ) : (
          <>
            {/*
              Short enough to sit beside the price on a phone. The confirmation
              is the widest of the labels, and a bar that grows for two seconds
              every time something is added is a bar that moves the button out
              from under the thumb that just pressed it.
            */}
            <span className="md:hidden">{added ? 'اضافه شد' : 'افزودن به سبد'}</span>
            <span className="hidden md:inline">
              {added ? 'به سبد خرید اضافه شد' : 'افزودن به سبد خرید'}
            </span>
          </>
        )}
      </Button>
    </StickyActionBar>
  );
}
