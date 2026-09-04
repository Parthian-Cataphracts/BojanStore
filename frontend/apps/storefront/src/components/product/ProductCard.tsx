'use client';

import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';
import { Badge, Icon, Price, cn } from '@bojan/ui';
import { ProductImage } from '@/components/product/ProductImage';
import { useCart } from '@/lib/cart/store';
import { useWishlist } from '@/lib/wishlist/store';
import { routes } from '@/lib/routes';
import type { Product } from '@/lib/api/types';

export interface ProductCardProps {
  product: Product;
  /** Fixes the card width for horizontal rails; grids leave this off. */
  railWidth?: boolean;
  priority?: boolean;
}

/**
 * The catalogue card from screens 04 / 21 / 23 / 24: warm paper surface,
 * square image that scales on hover, wishlist toggle pinned to the far corner,
 * price and quick-add on the footer row.
 *
 * Both controls are live. They were the two most-repeated dead buttons in the
 * storefront — this card renders in every grid and rail on the site.
 */
export function ProductCard({ product, railWidth = false, priority = false }: ProductCardProps) {
  const { addItem } = useCart();
  const { has, toggle } = useWishlist();
  const [added, setAdded] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  /*
    Sold out, for whichever kind of product this is.

    A product sold by combination has no stock of its own worth reading — the
    parent's number counts nothing anybody can buy. `defaultSku` is the first
    combination that still has stock, so its absence on such a product is what
    «sold out» means there.
  */
  const soldOut = product.hasVariants ? !product.defaultSku : product.stock === 0;
  const saved = has(product.id);

  useEffect(() => {
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, []);

  /*
    One tap: reserve something real, say so, and go back to being a plus.

    A tile has no variant picker and no room to grow one, so this used to add
    the plain product: «a brush», with no size on the line, the stock taken off
    the parent rather than off the one the shopper meant, and every size they
    went on to try collapsing into that same line. Sending them to the product
    page to choose instead was correct and slower than they asked for — the
    point of this button is one tap.

    So the tap reserves the first combination the shop still has, at its own
    price, on its own line. Choosing a different one is a tap away on the
    product page or in the basket, and until then the shopper has the thing
    rather than a link to it.
  */
  function quickAdd() {
    addItem(product, 1, product.defaultSku);

    // The design gives this button no room for a label, so the icon itself
    // acknowledges the click and then goes back.
    setAdded(true);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => setAdded(false), 1500);
  }

  return (
    <article
      className={`paper-card group relative flex flex-col overflow-hidden rounded-lg transition-shadow duration-300 hover:shadow-soft ${
        /*
          Fixed on a phone and a tablet, where a rail is swiped and a card that
          overflows the edge is the affordance saying so. From `xl` the width
          is a fifth of the rail instead: a shelf on a desktop is looked at
          rather than swiped, and 220px cards left four of them sitting in a
          1312px row with half of a fifth cut off at the end — which reads as a
          rendering fault rather than as more to come. 64px is the four
          16px gutters between the five.
        */
        railWidth ? 'w-[168px] shrink-0 md:w-[220px] xl:w-[calc((100%-64px)/5)]' : ''
      }`}
    >
      <button
        type="button"
        aria-pressed={saved}
        aria-label={
          saved
            ? `حذف ${product.title} از علاقه‌مندی‌ها`
            : `افزودن ${product.title} به علاقه‌مندی‌ها`
        }
        onClick={() => toggle(product)}
        className={cn(
          'absolute end-sm top-sm z-10 flex h-8 w-8 items-center justify-center rounded-full bg-surface-container-lowest/80 backdrop-blur-sm transition-colors hover:text-secondary',
          saved ? 'text-secondary' : 'text-outline',
        )}
      >
        <Icon name="favorite" size={20} filled={saved} />
      </button>

      <Link
        href={routes.product(product.slug)}
        className="relative block aspect-square w-full overflow-hidden bg-surface-container-highest"
      >
        <ProductImage
          src={product.image}
          alt={product.imageAlt || product.title}
          fill
          priority={priority}
          sizes={railWidth ? '220px' : '(max-width: 768px) 50vw, (max-width: 1200px) 25vw, 280px'}
          className="object-cover transition-transform duration-500 group-hover:scale-105"
        />

        {(product.isNew || soldOut) && (
          <span className="absolute start-sm top-sm">
            {soldOut ? (
              <Badge tone="neutral">ناموجود</Badge>
            ) : (
              <Badge tone="mint">جدید</Badge>
            )}
          </span>
        )}
      </Link>

      <div className="flex flex-1 flex-col p-sm">
        <span className="mb-xs text-caption text-outline">{product.brand}</span>

        <h3 className="mb-xs line-clamp-2 text-product-title text-on-surface">
          <Link href={routes.product(product.slug)} className="transition-colors hover:text-primary">
            {product.title}
          </Link>
        </h3>

        <div className="mt-auto flex items-end justify-between gap-xs pt-sm">
          {/*
            A product with combinations is priced by them, and the figure here
            is the cheapest one the API keeps in step with the sizes. Printed
            bare it reads as the price; «از» is what makes it a starting price,
            so a shopper who wants the larger size is not surprised on the
            product page.
          */}
          <Price
            value={product.price}
            {...(product.compareAtPrice ? { compareAt: product.compareAtPrice } : null)}
            size="sm"
            from={product.hasVariants}
            className="flex-col items-start gap-0"
          />

          <button
            type="button"
            disabled={soldOut}
            aria-label={`افزودن ${product.title} به سبد خرید`}
            onClick={quickAdd}
            className={cn(
              'flex h-8 w-8 shrink-0 items-center justify-center rounded-full transition-colors disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-surface-container disabled:hover:text-primary',
              added
                ? 'bg-primary text-on-primary'
                : 'bg-surface-container text-primary hover:bg-primary hover:text-on-primary',
            )}
          >
            <Icon name={added ? 'check' : 'add'} size={20} />
          </button>
        </div>
      </div>
    </article>
  );
}
