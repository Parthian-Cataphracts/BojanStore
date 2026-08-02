'use client';

import { useState, type FormEvent } from 'react';
import { Button, Card, Code, Icon, cn, formatPrice } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import { useCart } from '@/lib/cart/store';
import type { Coupon } from '@/lib/api/types';

type Result = { kind: 'ok'; message: string } | { kind: 'error'; message: string } | null;

/**
 * Screen 76 — Apply a discount code.
 *
 * The code is checked by `/api/cart/coupon`, which owns the discount and the
 * attempt limit; a valid one is written into the cart, so the summary on the
 * checkout screens picks it up rather than this screen keeping its own copy.
 */
export function CouponForm({ coupons }: { coupons: Coupon[] }) {
  const { cart, applyCoupon, clearCoupon } = useCart();
  const [code, setCode] = useState('');
  const [result, setResult] = useState<Result>(null);
  const [checking, setChecking] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const entered = code.trim().toUpperCase();
    if (!entered) return;

    if (cart.lines.length === 0) {
      setResult({ kind: 'error', message: 'سبد خرید شما خالی است.' });
      return;
    }

    setChecking(true);
    setResult(null);
    try {
      // The lines travel with the code: the API re-prices the discount from
      // them and never trusts `subtotal`, so sending none priced every coupon
      // against an empty basket and rejected any code with a minimum order.
      // CheckoutForm was fixed for this; this screen was missed.
      const applied = await postJson<{ code: string; discount: number }>('/api/cart/coupon', {
        code: entered,
        subtotal: cart.subtotal,
        lines: cart.lines.map((line) => ({
          productId: line.productId,
          quantity: line.quantity,
        })),
      });

      applyCoupon(applied.code, applied.discount);
      setResult({
        kind: 'ok',
        message: `کد «${applied.code}» اعمال شد — ${formatPrice(applied.discount)} تخفیف.`,
      });
      setCode('');
    } catch (cause) {
      clearCoupon();
      setResult({
        kind: 'error',
        message: cause instanceof Error ? cause.message : 'کد تخفیف وارد شده معتبر نیست.',
      });
    } finally {
      setChecking(false);
    }
  }

  // The customer's own codes, from /me/coupons. This listed the fixture, so
  // every shopper saw the same invented codes under "کدهای فعال شما" and
  // whichever they tapped was refused on validation.
  const active = coupons.filter(
    (coupon) => !coupon.used && new Date(coupon.expiresAt).getTime() >= Date.now(),
  );

  return (
    <div className="flex flex-col gap-lg">
      <Card className="flex flex-col gap-md p-lg">
        <form onSubmit={submit} className="flex flex-col gap-sm">
          <label htmlFor="coupon" className="text-label-md font-medium text-on-surface-variant">
            کد تخفیف خود را وارد کنید
          </label>

          <div className="flex gap-sm">
            <input
              id="coupon"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              placeholder="BOJAN20"
              className="latin h-12 flex-1 rounded-lg border border-outline-variant bg-surface-container-lowest px-md text-body-md uppercase text-on-surface placeholder:normal-case focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/30"
            />
            <Button type="submit" loading={checking} className="px-xl">
              اعمال
            </Button>
          </div>
        </form>

        {result && (
          <p
            role="status"
            className={cn(
              'flex items-start gap-xs rounded-lg px-md py-sm text-body-md',
              result.kind === 'ok'
                ? 'bg-soft-mint text-primary'
                : 'bg-error-container text-on-error-container',
            )}
          >
            <Icon name={result.kind === 'ok' ? 'check_circle' : 'error'} size={20} className="mt-px shrink-0" />
            {result.message}
          </p>
        )}
      </Card>

      {active.length > 0 && (
        <section className="flex flex-col gap-md">
          <h2 className="font-headline text-card-title text-primary">کدهای فعال شما</h2>

          <Card className="divide-y divide-paper-border">
            {active.map((coupon) => (
              <button
                key={coupon.id}
                type="button"
                onClick={() => setCode(coupon.code)}
                className="flex w-full items-center justify-between gap-md p-md text-start transition-colors hover:bg-surface-container-low"
              >
                <span className="flex min-w-0 flex-col gap-xs">
                  <span className="text-body-md text-on-surface">{coupon.title}</span>
                  <span className="text-caption text-on-surface-variant">{coupon.condition}</span>
                </span>
                <Code className="shrink-0 rounded border border-dashed border-primary/40 px-sm py-xs text-caption font-semibold text-primary">
                  {coupon.code}
                </Code>
              </button>
            ))}
          </Card>
        </section>
      )}
    </div>
  );
}
