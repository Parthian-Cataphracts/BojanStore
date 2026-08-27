'use client';

import Link from 'next/link';
import { Badge, Icon, Price, cn, toPersianDigits } from '@bojan/ui';
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
  const { cart, addItem } = useCart();
  const { has, toggle } = useWishlist();
  const soldOut = product.stock === 0;
  const saved = has(product.id);

  /*
    How many of this are already in the basket, if any.

    The button used to answer a tap with a tick for a second and a half and
    then forget, so a shopper filling a grid could not tell what they had
    already taken and had to wait out the tick before the button looked
    pressable again. The count is both answers at once: it says the tap landed,
    it says how many are in there, and it is true for as long as it is true
    rather than for 1500ms.

    The card has no variant picker, so this is the plain line — a product added
    from a grid is added without a SKU, which is the line `quickAdd` makes.
  */
  const inCart = cart.lines.find(
    (line) => line.productId === product.id && line.skuId === undefined,
  );

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
          <Price
            value={product.price}
            {...(product.compareAtPrice ? { compareAt: product.compareAtPrice } : null)}
            size="sm"
            className="flex-col items-start gap-0"
          />

          {/*
            A product sold by combination is chosen on its own page, not from
            here.

            This card has no room for a size picker and no way to grow one, so
            adding from it put the plain product in the basket: a shopper
            browsing a grid of brushes added «a brush», with no size on the line
            and the stock taken off the parent rather than off the one they
            meant — and every size they tried collapsed into that same line. The
            control keeps its place and its shape and becomes the way to the
            page that can ask the question.
          */}
          {product.hasVariants ? (
            <Link
              href={routes.product(product.slug)}
              aria-label={`انتخاب گزینه و افزودن ${product.title} به سبد خرید`}
              className={cn(
                'flex h-8 w-8 shrink-0 items-center justify-center rounded-full transition-colors',
                'bg-surface-container text-primary hover:bg-primary hover:text-on-primary',
                soldOut && 'pointer-events-none opacity-40',
              )}
            >
              {/* Forward, which in RTL points left. */}
              <Icon name="chevron_left" size={20} />
            </Link>
          ) : (
            <button
              type="button"
              disabled={soldOut}
              aria-label={`افزودن ${product.title} به سبد خرید`}
              onClick={() => addItem(product, 1)}
              className={cn(
                'tabular flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-body-md font-label-md transition-colors disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-surface-container disabled:hover:text-primary',
                inCart
                  ? 'bg-primary text-on-primary'
                  : 'bg-surface-container text-primary hover:bg-primary hover:text-on-primary',
              )}
            >
              {inCart ? toPersianDigits(inCart.quantity) : <Icon name="add" size={20} />}
            </button>
          )}
        </div>
      </div>
    </article>
  );
}
