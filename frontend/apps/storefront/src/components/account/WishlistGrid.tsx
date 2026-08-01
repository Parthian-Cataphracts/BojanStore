'use client';

import Image from 'next/image';
import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';
import { EmptyState, Icon, Price, ProductCardSkeleton, buttonClasses, cn } from '@bojan/ui';
import { useCart } from '@/lib/cart/store';
import { useWishlist } from '@/lib/wishlist/store';
import { routes } from '@/lib/routes';

/**
 * Screen 11's card grid.
 *
 * The list comes from the wishlist store rather than a prop, so unsaving here
 * and unsaving from a catalogue card are the same action — the heart on every
 * product card reflects it immediately.
 */
export function WishlistGrid() {
  const { items, hydrated, remove } = useWishlist();
  const { addItem } = useCart();
  const [addedId, setAddedId] = useState<string | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, []);

  function quickAdd(product: (typeof items)[number]) {
    addItem(product, 1);
    setAddedId(product.id);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => setAddedId(null), 1500);
  }

  // The list is read from storage after mount; showing the empty state during
  // that first paint would flash "your wishlist is empty" at someone who has one.
  if (!hydrated) {
    return (
      <div className="grid grid-cols-2 gap-md md:grid-cols-4">
        <ProductCardSkeleton />
        <ProductCardSkeleton />
        <ProductCardSkeleton />
        <ProductCardSkeleton />
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <EmptyState
        icon="favorite"
        title="لیست علاقه‌مندی‌ها خالی شد"
        description="هر محصولی را که پسندیدید با زدن قلب ذخیره کنید."
        action={
          <Link href={routes.products} className={buttonClasses()}>
            مشاهده محصولات
          </Link>
        }
      />
    );
  }

  return (
    <div className="grid grid-cols-2 gap-md md:grid-cols-4">
      {items.map((product) => (
        <article
          key={product.id}
          className="paper-card group relative flex flex-col overflow-hidden rounded-lg shadow-ambient transition-shadow hover:shadow-soft"
          data-product-id={product.id}
        >
          <button
            type="button"
            aria-label={`حذف ${product.title} از علاقه‌مندی‌ها`}
            onClick={() => remove(product.id)}
            className="absolute end-2 top-2 z-10 rounded-full bg-surface-container-lowest/80 p-1 text-secondary-container shadow-sm backdrop-blur-sm transition-transform hover:scale-110"
          >
            <Icon name="favorite" filled size={20} />
          </button>

          <Link
            href={routes.product(product.slug)}
            className="relative block aspect-square w-full border-b border-paper-border bg-surface-container-highest"
          >
            <Image
              src={product.image}
              alt={product.imageAlt || product.title}
              fill
              sizes="(max-width: 768px) 50vw, 25vw"
              className="object-cover"
            />
          </Link>

          <div className="flex flex-1 flex-col justify-between gap-md p-3">
            <div className="flex flex-col gap-1">
              <h3 className="line-clamp-2 text-label-md font-label-md text-on-surface">
                <Link href={routes.product(product.slug)} className="hover:text-primary">
                  {product.title}
                </Link>
              </h3>
              <Price
                value={product.price}
                {...(product.compareAtPrice ? { compareAt: product.compareAtPrice } : null)}
                size="sm"
              />
            </div>

            <div className="flex justify-end">
              <button
                type="button"
                disabled={product.stock === 0}
                aria-label={`افزودن ${product.title} به سبد خرید`}
                onClick={() => quickAdd(product)}
                className={cn(
                  'flex items-center justify-center rounded-full p-2 shadow-sm transition-colors disabled:cursor-not-allowed disabled:opacity-40',
                  addedId === product.id
                    ? 'bg-primary-container text-on-primary'
                    : 'bg-primary text-on-primary hover:bg-primary-container',
                )}
              >
                <Icon name={addedId === product.id ? 'check' : 'add_shopping_cart'} size={18} />
              </button>
            </div>
          </div>
        </article>
      ))}
    </div>
  );
}
