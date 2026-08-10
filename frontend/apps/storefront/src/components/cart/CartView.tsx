'use client';

import Image from 'next/image';
import Link from 'next/link';
import { useMemo } from 'react';
import {
  Card,
  EmptyState,
  Icon,
  IconButton,
  Price,
  ProductCardSkeleton,
  QuantityStepper,
  buttonClasses,
  formatPrice,
} from '@bojan/ui';
import { MAX_CART_QUANTITY, useCart } from '@/lib/cart/store';
import { routes } from '@/lib/routes';

/** Screen 07 — Cart. */
export function CartView() {
  const { cart, hydrated, setQuantity, removeItem } = useCart();
  const lines = cart.lines;

  // "Savings" is the only figure the summary derives rather than reads: it is
  // the gap between list and selling price, which the cart totals do not carry.
  const savings = useMemo(
    () =>
      lines.reduce(
        (sum, line) =>
          line.compareAtPrice ? sum + (line.compareAtPrice - line.unitPrice) * line.quantity : sum,
        0,
      ),
    [lines],
  );

  // The basket is read from storage after mount. Showing the empty state during
  // that first paint would flash "your cart is empty" at someone who has one.
  if (!hydrated) {
    return (
      <div className="grid gap-lg lg:grid-cols-[1fr_360px] lg:items-start">
        <div className="flex min-w-0 flex-col gap-md">
          <ProductCardSkeleton />
          <ProductCardSkeleton />
        </div>
      </div>
    );
  }

  if (lines.length === 0) {
    return (
      <EmptyState
        icon="shopping_cart"
        title="سبد خرید شما خالی است"
        description="هنوز محصولی اضافه نکرده‌اید. از دسته‌بندی‌ها شروع کنید و چیزهای خوب را پیدا کنید."
        action={
          <Link href={routes.products} className={buttonClasses()}>
            شروع خرید
          </Link>
        }
      />
    );
  }

  return (
    <div className="grid gap-lg lg:grid-cols-[1fr_360px] lg:items-start">
      {/* `min-w-0`: a grid item will not shrink below its widest child unless
          told to, so one long product title would widen the whole page. */}
      <ul className="flex min-w-0 flex-col gap-md">
        {lines.map((line) => (
          <li key={line.id}>
            <Card className="flex gap-md p-md">
              <Link
                href={routes.product(line.slug)}
                className="relative h-24 w-24 shrink-0 overflow-hidden rounded-lg bg-surface-container-highest"
              >
                <Image src={line.image} alt={line.title} fill sizes="96px" className="object-cover" />
              </Link>

              <div className="flex min-w-0 flex-1 flex-col gap-xs">
                <span className="text-caption text-outline">{line.brand}</span>
                <Link
                  href={routes.product(line.slug)}
                  className="line-clamp-2 text-body-md text-on-surface transition-colors hover:text-primary"
                >
                  {line.title}
                </Link>

                <div className="mt-auto flex flex-wrap items-center justify-between gap-sm pt-sm">
                  {/* The ceiling the product page already applies. Without it
                      the stepper's own default of 99 let a shopper take a
                      two-in-stock item to twenty here, and the order was
                      refused at the last screen of the checkout. */}
                  <QuantityStepper
                    value={line.quantity}
                    max={Math.min(MAX_CART_QUANTITY, line.stock && line.stock > 0 ? line.stock : MAX_CART_QUANTITY)}
                    onChange={(next) => setQuantity(line.id, next)}
                  />
                  <Price
                    value={line.unitPrice * line.quantity}
                    {...(line.compareAtPrice
                      ? { compareAt: line.compareAtPrice * line.quantity }
                      : null)}
                  />
                </div>
              </div>

              <IconButton
                icon="delete"
                label={`حذف ${line.title}`}
                onClick={() => removeItem(line.id)}
                className="self-start text-outline hover:text-error"
              />
            </Card>
          </li>
        ))}
      </ul>

      {/* Order summary — sticky beside the list on desktop. */}
      <Card className="flex flex-col gap-md p-lg lg:sticky lg:top-24">
        <h2 className="font-headline text-display-md text-primary">خلاصه سفارش</h2>

        <dl className="flex flex-col gap-sm text-body-md">
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">جمع کالاها</dt>
            <dd className="tabular text-on-surface">{formatPrice(cart.subtotal)}</dd>
          </div>

          {savings > 0 && (
            <div className="flex items-center justify-between">
              <dt className="text-on-surface-variant">سود شما از تخفیف</dt>
              <dd className="tabular text-secondary">{formatPrice(savings)}</dd>
            </div>
          )}

          {cart.discount > 0 && (
            <div className="flex items-center justify-between">
              <dt className="flex items-center gap-xs text-on-surface-variant">
                <Icon name="sell" size={16} />
                کد تخفیف {cart.couponCode}
              </dt>
              <dd className="tabular text-secondary">−{formatPrice(cart.discount)}</dd>
            </div>
          )}

          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">هزینه ارسال</dt>
            <dd className="tabular text-on-surface">{formatPrice(cart.shipping)}</dd>
          </div>

          <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
            <dt className="text-body-lg font-label-md text-primary">مبلغ قابل پرداخت</dt>
            <dd className="tabular text-body-lg font-label-md text-primary">
              {formatPrice(cart.total)}
            </dd>
          </div>
        </dl>

        <Link href={routes.checkout} className={buttonClasses({ size: 'lg', fullWidth: true })}>
          ادامه فرایند خرید
        </Link>

        <Link
          href={routes.products}
          className="text-center text-label-md font-label-md text-on-surface-variant transition-colors hover:text-primary"
        >
          ادامه خرید
        </Link>
      </Card>
    </div>
  );
}
