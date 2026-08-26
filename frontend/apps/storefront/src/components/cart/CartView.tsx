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
  cn,
  formatPrice,
  toPersianDigits,
} from '@bojan/ui';
import { StickyActionBar } from '@/components/layout/StickyActionBar';
import { MAX_CART_QUANTITY, useCart } from '@/lib/cart/store';
import { routes } from '@/lib/routes';

/** Screen 07 — Cart. */
export function CartView() {
  const { cart, count, hydrated, setQuantity, removeItem } = useCart();
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
                {/*
                  Smaller and tighter on a phone than the 16px/1.8 body copy the
                  page is set in. Two lines of that measure 58px, which is a
                  third of the row spent on a title the shopper already knows —
                  they put the thing in the basket themselves.
                */}
                <Link
                  href={routes.product(line.slug)}
                  className="line-clamp-2 text-caption leading-6 text-on-surface transition-colors hover:text-primary md:text-body-md"
                >
                  {line.title}
                </Link>

                {/*
                  Together on a wide card rather than flung to its two ends: at
                  the width the desktop cart gives a row, `justify-between` put
                  half a metre between the stepper and the price it changes.
                */}
                <div className="mt-auto flex flex-wrap items-center justify-between gap-sm pt-sm md:justify-start md:gap-xl">
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

        {/*
          One way out of the summary, not two. «ادامه فرایند خرید» and «ادامه
          خرید» read as the same sentence to anyone not looking closely, and a
          shopper who wants to keep browsing has the whole header above.

          Hidden until `lg` because below it the bar at the bottom of the screen
          is carrying the same button, and two of them is the thing this line
          just finished removing.
        */}
        <Link
          href={routes.checkout}
          // After the button classes, not before: `hidden` and the button's own
          // `inline-flex` are the same property, and tailwind-merge keeps
          // whichever comes last — put first, the hiding was thrown away.
          className={cn(buttonClasses({ size: 'lg', fullWidth: true }), 'hidden lg:inline-flex')}
        >
          ثبت سفارش
        </Link>
      </Card>

      {/*
        The phone's checkout bar.

        The summary sits under the basket on a narrow screen, so its button
        started 1071px down the page with three items in the cart and further
        with five — a shopper had to scroll past everything they had chosen to
        find the way out. The total travels with the button because the number
        is the reason anyone presses it.
      */}
      <StickyActionBar
        inlineFrom="lg"
        /*
          Shorter than the default bar: this one carries a price and a button,
          not a form's worth of controls, and every pixel of it is a pixel of
          basket the shopper cannot see.

          Reversed for the same reason the product page's bar is — total on the
          left, button on the right, as the shops shoppers already use have
          them, rather than the mirror image an RTL row would draw.
        */
        className="flex-row-reverse items-center justify-between py-sm lg:hidden"
      >
        {/* `items-end` is the left edge in RTL — the side of the bar this
            block is on, so the count and the total line up with each other. */}
        <span className="flex flex-col items-end">
          <span className="text-caption text-on-surface-variant">
            جمع {toPersianDigits(count)} کالا
          </span>
          <span className="tabular text-body-md font-label-md text-primary">
            {formatPrice(cart.total)}
          </span>
        </span>

        <Link
          href={routes.checkout}
          className={buttonClasses({ className: 'min-w-[10rem]' })}
        >
          ثبت سفارش
        </Link>
      </StickyActionBar>
    </div>
  );
}
