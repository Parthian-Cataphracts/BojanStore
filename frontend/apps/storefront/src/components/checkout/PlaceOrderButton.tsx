'use client';

import { useRouter } from 'next/navigation';
import { useRef, useState } from 'react';
import { Icon, buttonClasses } from '@bojan/ui';
import { newIdempotencyKey, postJson } from '@/lib/api/submit';
import { useCart } from '@/lib/cart/store';
import { useCheckout } from '@/lib/checkout/store';
import { routes } from '@/lib/routes';

/**
 * Screen 78's primary action — the one that actually places the order.
 *
 * It used to be a `<Link>` straight to the payment-success screen. Nothing was
 * submitted anywhere: the guided flow collected an address, a shipping method,
 * a delivery window and a payment method across four screens and then showed a
 * success page for an order that did not exist.
 *
 * The basket comes from the cart store and the choices from the checkout store,
 * so this posts the same body screen 08 does, to the same route, and the API
 * prices and validates it identically. On success the basket and the
 * selections are cleared — leaving either behind would let a reload place the
 * order a second time against a basket that was already bought.
 */
export function PlaceOrderButton() {
  const router = useRouter();
  const { cart, hydrated: cartReady, clear } = useCart();
  const { selection, hydrated: selectionReady, reset } = useCheckout();

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // One key for this shopper's attempt at buying this basket, minted on the
  // first submit and kept for any retry after a failure. Retrying is safe under
  // the same key precisely because a failed attempt writes no order for it to
  // match; what the key prevents is the *successful* order being placed twice
  // by a double-tap or a resubmitted request. It is deliberately not derived
  // from the basket — that is the API's fallback, and it would make an honest
  // repeat purchase of the same items resolve to the original order.
  const attemptKey = useRef<string | null>(null);

  const ready = cartReady && selectionReady;
  const empty = ready && cart.lines.length === 0;

  // Each is chosen on its own step; missing one means that step was skipped.
  const missing = ready
    ? [
        !selection.addressId ? { label: 'آدرس تحویل', href: routes.checkoutAddress } : null,
        !selection.shippingMethodId ? { label: 'روش ارسال', href: routes.checkoutShipping } : null,
        !selection.paymentMethodId ? { label: 'روش پرداخت', href: routes.checkoutPayment } : null,
      ].filter((step) => step !== null)
    : [];

  const canSubmit = ready && !empty && missing.length === 0 && !submitting;

  async function submit() {
    if (!canSubmit) return;

    setSubmitting(true);
    setError(null);
    attemptKey.current ??= newIdempotencyKey();

    try {
      const placed = await postJson<{ orderNumber: string; paymentUrl?: string }>('/api/orders', {
        lines: cart.lines.map((line) => ({
          productId: line.productId,
          quantity: line.quantity,
          ...(line.skuId ? { skuId: line.skuId } : null),
        })),
        addressId: selection.addressId,
        shippingMethodId: selection.shippingMethodId,
        paymentMethodId: selection.paymentMethodId,
        ...(cart.couponCode ? { couponCode: cart.couponCode } : null),
        ...(selection.note ? { note: selection.note } : null),
        ...(selection.deliveryWindow ? { deliveryWindow: selection.deliveryWindow } : null),
      }, { headers: { 'Idempotency-Key': attemptKey.current } });

      // Cleared before navigating: the order exists now, and a back-button
      // return to this screen must not be able to place it again.
      clear();
      reset();

      // A gateway method answers with a URL to hand the shopper to; cash on
      // delivery does not, and goes straight to the confirmation.
      if (placed.paymentUrl) {
        window.location.href = placed.paymentUrl;
        return;
      }

      router.push(routes.paymentSuccess);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت سفارش انجام نشد.');
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-sm">
      <button
        type="button"
        onClick={submit}
        disabled={!canSubmit}
        className={buttonClasses({ size: 'lg', fullWidth: true, className: 'gap-sm' })}
      >
        <Icon name={submitting ? 'progress_activity' : 'lock'} size={20} />
        {submitting ? 'در حال ثبت سفارش…' : 'ثبت سفارش و پرداخت'}
      </button>

      {empty && (
        <p role="alert" className="flex items-center gap-xs text-caption text-error">
          <Icon name="error" size={16} />
          سبد خرید شما خالی است.
        </p>
      )}

      {/* Named, and linked: "something is missing" without saying what leaves
          the shopper to guess which of four screens to go back to. */}
      {missing.length > 0 && (
        <p role="alert" className="flex flex-wrap items-center gap-xs text-caption text-error">
          <Icon name="error" size={16} />
          ابتدا این مرحله‌ها را کامل کنید:
          {missing.map((step) => (
            <a key={step.href} href={step.href} className="underline underline-offset-4">
              {step.label}
            </a>
          ))}
        </p>
      )}

      {error && (
        <p role="alert" className="flex items-center gap-xs text-caption text-error">
          <Icon name="error" size={16} />
          {error}
        </p>
      )}
    </div>
  );
}
