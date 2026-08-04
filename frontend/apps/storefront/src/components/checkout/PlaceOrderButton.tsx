'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, Icon } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import type { PlacedOrder } from '@/lib/api/cart';
import { useCart } from '@/lib/cart/store';
import { useCheckoutSelection } from '@/lib/checkout/store';
import { routes } from '@/lib/routes';

/**
 * The guided checkout's final action — screen 78.
 *
 * This is the step that was missing entirely: the button under "تایید نهایی"
 * was a link to the success screen, so a shopper who walked the whole flow
 * reached a page saying their order was placed when nothing had been. It posts
 * the same body the single-page checkout does, to the same route, which
 * re-validates every part of it server-side.
 */
export function PlaceOrderButton({ fallbackAddressId }: { fallbackAddressId?: string }) {
  const router = useRouter();
  const { cart, hydrated, clear } = useCart();
  const { selection, hydrated: selectionReady, clear: clearSelection } = useCheckoutSelection();

  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const ready = hydrated && selectionReady;
  const addressId = selection.addressId ?? fallbackAddressId;

  async function submit() {
    if (cart.lines.length === 0) {
      setError('سبد خرید شما خالی است.');
      return;
    }

    if (!addressId) {
      setError('آدرس تحویل را انتخاب کنید.');
      return;
    }

    if (!selection.shippingMethodId) {
      setError('روش ارسال را انتخاب کنید.');
      return;
    }

    if (!selection.paymentMethodId) {
      setError('روش پرداخت را انتخاب کنید.');
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const order = await postJson<PlacedOrder>('/api/orders', {
        lines: cart.lines.map((line) => ({ productId: line.productId, quantity: line.quantity })),
        addressId,
        shippingMethodId: selection.shippingMethodId,
        paymentMethodId: selection.paymentMethodId,
        ...(cart.couponCode ? { couponCode: cart.couponCode } : null),
        ...(selection.note ? { note: selection.note } : null),
      });

      // The basket is spent, and so are the choices that were made about it —
      // leaving either behind would let a refresh re-order.
      clear();
      clearSelection();

      if (order.paymentUrl) {
        window.location.assign(order.paymentUrl);
        return;
      }

      router.push(`${routes.orderPlaced}?order=${encodeURIComponent(order.orderNumber)}`);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ثبت سفارش انجام نشد.');
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-sm">
      <Button
        type="button"
        size="lg"
        fullWidth
        loading={submitting}
        disabled={!ready}
        onClick={submit}
        trailingIcon="lock"
      >
        پرداخت و ثبت نهایی سفارش
      </Button>

      {error && (
        <span role="alert" className="flex items-center gap-xs text-caption text-error">
          <Icon name="error" size={16} />
          {error}
        </span>
      )}
    </div>
  );
}
