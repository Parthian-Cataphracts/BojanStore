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
 * The two screens carry different controls, because the fault they had was not
 * the same fault.
 *
 * Inline on desktop the bar sits directly under the price and has always shown
 * a stepper beside the button: the shopper says «three» and then adds three.
 * That is left exactly as it was.
 *
 * Floating over a phone it is usually the only part of the page on screen — the
 * shopper has scrolled down to the reviews — so it carries the price and had
 * room for nothing else. There was no stepper at all there, so the only way to
 * say «two» was to press the button twice, and the button answered each press
 * by turning into «اضافه شد» for two seconds. Nothing was in flight; the wait
 * was the label, and it made the second press feel refused.
 *
 * So on a phone the button is replaced by the line's own quantity once the line
 * exists: plus adds one, and at one the minus becomes a trash that takes it
 * back out. The number is the acknowledgement, so nothing has to be said and
 * nothing has to time out.
 *
 * Per variant, not per product. The basket keys a line on the product and the
 * chosen SKU together, so a page offering three sizes has three lines to hold
 * three quantities, and picking a different size here swaps the control over to
 * that one's.
 *
 * Both controls are in the markup and Tailwind hides one, which is how every
 * other responsive switch in this app is made — `StickyActionBar` included.
 */
export function AddToCartBar({
  product,
  sku,
  requiresSku = false,
}: {
  product: Product;
  sku?: ProductSku;
  /**
   * The product is sold by combination, so nothing may be bought until the pick
   * resolves to one — see `ProductPurchase`.
   */
  requiresSku?: boolean;
}) {
  const { cart, addItem, changeQuantity, removeItem } = useCart();

  // Desktop's own quantity, chosen before anything is added. Untouched.
  const [quantity, setQuantity] = useState(1);
  const [added, setAdded] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // A variant with no matching SKU (an incomplete catalogue entry, or mock
  // mode where SKUs never load) falls back to the product's own stock — the
  // same number the shop sells from when there is no variant to pick.
  const stock = sku?.stock ?? product.stock;

  /*
    Nothing to sell: either the chosen combination has none left, or the product
    is sold by combination and this pick is not one of them.

    The second half is the one that was missing. A size an operator listed but
    never gave a combination fell through to the product's own price and stock
    and added a line carrying no SKU at all — so the shopper believed they had
    bought that size, the order recorded none, and it came off the plain
    product's shelf. Worse, every such size shared that one line, because a line
    is keyed on the product and the SKU together and they all had the same
    absent SKU: picking «19» and then «20» did not make two lines, it made two
    of whatever the product is.
  */
  const soldOut = stock === 0 || (requiresSku && !sku);
  /*
    A picked variant prices itself, and that now includes what it is struck
    through against.

    The list price used to be the product's regardless of which combination was
    chosen, so a discount on one size drew a strike-through across every other
    size — a shopper looking at the full-price size 4 was shown size 2's saving.
    A combination carries its own `compareAt`, and the product's stands only for
    a line that names no combination at all.
  */
  const price = sku?.price ?? product.price;
  const compareAt = sku ? (sku.compareAt ?? undefined) : product.compareAtPrice;

  // The same pair the cart's reducer matches on, so the phone's control and the
  // line it edits can never disagree about which line that is.
  const line = cart.lines.find(
    (candidate) => candidate.productId === product.id && candidate.skuId === sku?.id,
  );

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

      {/* --- the phone's control ------------------------------------------- */}

      {line && !soldOut ? (
        /*
          Already in the basket: its quantity, in place of the button that put
          it there. Sized to the basis the button leaves so the bar does not
          change width when it swaps — a control that resizes under the thumb
          that just pressed it is the fault the two-second label had.
        */
        <QuantityStepper
          value={line.quantity}
          // Steps rather than absolute values, so tapping «+» three times
          // quickly adds three — see `onStep`.
          onStep={(delta) => changeQuantity(line.id, delta)}
          onChange={(next) => changeQuantity(line.id, next - line.quantity)}
          onRemove={() => removeItem(line.id)}
          // The stock this variant actually has, so the control cannot count
          // past what the checkout will accept.
          max={Math.max(1, stock)}
          /*
            `rounded-lg`, not the stepper's own `rounded-full`. This one stands
            where the add-to-cart button stood a tap earlier and is the same
            height, so a pill there reads as a different kind of control
            arriving in the same place. The cart's rows keep the pill: nothing
            swaps with them.
          */
          className="h-12 flex-1 basis-[9rem] justify-between rounded-lg md:hidden"
        />
      ) : (
        <Button
          size="lg"
          fullWidth
          // A definite basis, not `auto`: `fullWidth` puts `w-full` on the
          // button, and a flex item with `basis-auto` takes that 100% as its
          // basis and wraps onto its own row every time. `md:hidden` last,
          // because it and the button's own `inline-flex` are the same
          // property and tailwind-merge keeps whichever comes after.
          className="flex-1 basis-[9rem] whitespace-nowrap md:hidden"
          disabled={soldOut}
          icon="shopping_cart"
          onClick={() => addItem(product, 1, sku)}
        >
          {soldOut ? 'ناموجود' : 'افزودن به سبد'}
        </Button>
      )}

      {/* --- desktop, as it was -------------------------------------------- */}

      <QuantityStepper
        value={quantity}
        onChange={setQuantity}
        max={Math.max(1, stock)}
        disabled={soldOut}
        className="hidden md:flex"
      />

      {/*
        A basis wide enough for the longest of the labels — «به سبد خرید اضافه
        شد» — rather than `w-full`'s «all of it», so the label stays on one
        line, which a button with a fixed height has no room to do otherwise.
      */}
      <Button
        size="lg"
        fullWidth
        className="hidden whitespace-nowrap md:inline-flex md:flex-1 md:basis-[13rem]"
        disabled={soldOut}
        icon={added ? 'check' : 'shopping_cart'}
        onClick={addToCart}
      >
        {soldOut ? 'ناموجود' : added ? 'به سبد خرید اضافه شد' : 'افزودن به سبد خرید'}
      </Button>
    </StickyActionBar>
  );
}
