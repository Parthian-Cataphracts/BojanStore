'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Button, Card, Icon, Input, Select, Textarea, buttonClasses, formatPrice } from '@bojan/ui';
import { postJson } from '@/lib/api/submit';
import type { Address } from '@/lib/api/types';
import type {
  CheckoutPaymentMethod,
  CheckoutShippingMethod,
  PlacedOrder,
} from '@/lib/api/cart';
import { useCart } from '@/lib/cart/store';
import { routes } from '@/lib/routes';

/**
 * Screen 08 — Checkout.
 *
 * The shipping and payment options arrive as props, resolved server-side from
 * the same endpoints the order route validates against. They used to be read
 * from a fixture module, which meant the fee drawn in the summary was a
 * constant while the order was charged the shipping tier's real price — two
 * different numbers on the one screen where they have to agree — and a tier an
 * operator had deactivated was still offered.
 *
 * Nothing on this screen decides the order. It posts to `/api/orders`, which
 * re-validates the basket, the address and both method ids against the server's
 * own data before anything is placed.
 */
export function CheckoutForm({
  addresses,
  shippingMethods,
  paymentMethods,
}: {
  addresses: Address[];
  shippingMethods: CheckoutShippingMethod[];
  paymentMethods: CheckoutPaymentMethod[];
}) {
  const router = useRouter();
  const { cart, hydrated, applyCoupon, clearCoupon, clear } = useCart();

  const [addressId, setAddressId] = useState(
    () => addresses.find((address) => address.isDefault)?.id ?? addresses[0]?.id ?? '',
  );
  const [shippingId, setShippingId] = useState(() => shippingMethods[0]?.id ?? '');
  const [paymentId, setPaymentId] = useState(() => paymentMethods[0]?.id ?? '');
  const [couponInput, setCouponInput] = useState('');
  const [couponError, setCouponError] = useState<string | null>(null);
  const [checkingCoupon, setCheckingCoupon] = useState(false);
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // The chosen method's fee replaces the default the cart was seeded with.
  const shipping = shippingMethods.find((method) => method.id === shippingId);
  const shippingPrice = cart.lines.length > 0 ? (shipping?.price ?? 0) : 0;
  const total = cart.subtotal - cart.discount + shippingPrice;

  const empty = hydrated && cart.lines.length === 0;
  const canSubmit = !empty && hydrated && addressId !== '' && !submitting;

  async function applyCouponCode(event: FormEvent) {
    event.preventDefault();
    const code = couponInput.trim();
    if (!code) return;

    setCheckingCoupon(true);
    setCouponError(null);
    try {
      const result = await postJson<{ code: string; discount: number }>('/api/cart/coupon', {
        code,
        subtotal: cart.subtotal,
        lines: cart.lines.map((line) => ({ productId: line.productId, quantity: line.quantity })),
      });
      applyCoupon(result.code, result.discount);
      setCouponInput('');
    } catch (cause) {
      clearCoupon();
      setCouponError(cause instanceof Error ? cause.message : 'کد تخفیف معتبر نیست.');
    } finally {
      setCheckingCoupon(false);
    }
  }

  async function submit() {
    if (!canSubmit) {
      setError('برای ثبت سفارش، آدرس تحویل را انتخاب کنید.');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const order = await postJson<PlacedOrder>('/api/orders', {
        lines: cart.lines.map((line) => ({ productId: line.productId, quantity: line.quantity })),
        addressId,
        shippingMethodId: shippingId,
        paymentMethodId: paymentId,
        ...(cart.couponCode ? { couponCode: cart.couponCode } : null),
        ...(note.trim() ? { note: note.trim() } : null),
      });

      // The basket is spent — emptying it here stops a refresh re-ordering it.
      clear();

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
    <div className="grid gap-lg lg:grid-cols-[1fr_360px] lg:items-start">
      <div className="flex flex-col gap-lg">
        {/* 1 — Delivery address */}
        <Card className="flex flex-col gap-md p-lg">
          <h2 className="flex items-center gap-sm font-headline text-display-md text-primary">
            <Icon name="location_on" />
            آدرس تحویل
          </h2>

          <div className="flex flex-col gap-md">
            {addresses.map((address) => (
              <label
                key={address.id}
                className={`flex cursor-pointer gap-sm rounded-lg border p-md transition-colors ${
                  addressId === address.id
                    ? 'border-primary bg-soft-mint/30'
                    : 'border-outline-variant hover:bg-surface-container-low'
                }`}
              >
                <input
                  type="radio"
                  name="address"
                  className="mt-1 h-5 w-5 border-outline-variant text-primary focus:ring-2 focus:ring-primary/30"
                  checked={addressId === address.id}
                  onChange={() => setAddressId(address.id)}
                />
                <span className="flex flex-col gap-xs">
                  <span className="text-label-md font-label-md text-primary">{address.title}</span>
                  <span className="text-body-md text-on-surface-variant">
                    {address.province}، {address.city}، {address.line}
                  </span>
                  <span className="tabular text-caption text-outline">
                    {address.recipient} — {address.phone}
                  </span>
                </span>
              </label>
            ))}
          </div>

          <Link
            href={routes.checkoutNewAddress}
            className={buttonClasses({ variant: 'outline', className: 'self-start' })}
          >
            <Icon name="add" size={20} />
            افزودن آدرس جدید
          </Link>
        </Card>

        {/* 2 — Shipping method */}
        <Card className="flex flex-col gap-md p-lg">
          <h2 className="flex items-center gap-sm font-headline text-display-md text-primary">
            <Icon name="local_shipping" />
            روش ارسال
          </h2>

          {shippingMethods.map((method) => (
            <label
              key={method.id}
              className={`flex cursor-pointer items-center justify-between gap-md rounded-lg border p-md transition-colors ${
                shippingId === method.id
                  ? 'border-primary bg-soft-mint/30'
                  : 'border-outline-variant hover:bg-surface-container-low'
              }`}
            >
              <span className="flex items-center gap-sm">
                <input
                  type="radio"
                  name="shipping"
                  className="h-5 w-5 border-outline-variant text-primary focus:ring-2 focus:ring-primary/30"
                  checked={shippingId === method.id}
                  onChange={() => setShippingId(method.id)}
                />
                <span className="flex flex-col">
                  <span className="text-body-md text-on-surface">{method.title}</span>
                  {method.note && (
                    <span className="text-caption text-on-surface-variant">{method.note}</span>
                  )}
                </span>
              </span>
              <span className="tabular shrink-0 text-label-md font-label-md text-primary">
                {formatPrice(method.price)}
              </span>
            </label>
          ))}

          <Select label="زمان تحویل" defaultValue="any">
            <option value="any">بدون ترجیح زمانی</option>
            <option value="morning">صبح (۹ تا ۱۳)</option>
            <option value="afternoon">بعدازظهر (۱۴ تا ۱۹)</option>
          </Select>
        </Card>

        {/* 3 — Payment */}
        <Card className="flex flex-col gap-md p-lg">
          <h2 className="flex items-center gap-sm font-headline text-display-md text-primary">
            <Icon name="payments" />
            روش پرداخت
          </h2>

          {paymentMethods.map((method) => (
            <label
              key={method.id}
              className={`flex cursor-pointer items-center gap-sm rounded-lg border p-md transition-colors ${
                paymentId === method.id
                  ? 'border-primary bg-soft-mint/30'
                  : 'border-outline-variant hover:bg-surface-container-low'
              }`}
            >
              <input
                type="radio"
                name="payment"
                className="h-5 w-5 border-outline-variant text-primary focus:ring-2 focus:ring-primary/30"
                checked={paymentId === method.id}
                onChange={() => setPaymentId(method.id)}
              />
              <Icon name={method.icon} className="text-primary" />
              <span className="flex flex-col">
                <span className="text-body-md text-on-surface">{method.title}</span>
                {method.note && (
                  <span className="tabular text-caption text-on-surface-variant">{method.note}</span>
                )}
              </span>
            </label>
          ))}
        </Card>

        {/* 4 — Invoice + notes */}
        <Card className="flex flex-col gap-md p-lg">
          <h2 className="flex items-center gap-sm font-headline text-display-md text-primary">
            <Icon name="receipt_long" />
            اطلاعات تکمیلی
          </h2>

          <form onSubmit={applyCouponCode}>
            <Input
              label="کد تخفیف"
              placeholder="در صورت داشتن کد تخفیف وارد کنید"
              icon="local_offer"
              value={couponInput}
              onChange={(event) => setCouponInput(event.target.value)}
              {...(couponError ? { error: couponError } : null)}
              {...(cart.couponCode
                ? { hint: `کد «${cart.couponCode}» اعمال شد — ${formatPrice(cart.discount)} تخفیف.` }
                : null)}
              suffix={
                <button
                  type="submit"
                  disabled={checkingCoupon || couponInput.trim().length === 0}
                  className="pointer-events-auto text-label-md font-label-md text-primary transition-colors hover:text-secondary disabled:text-outline"
                >
                  اعمال
                </button>
              }
            />
          </form>

          <Textarea
            label="یادداشت سفارش"
            placeholder="توضیحی برای تحویل یا بسته‌بندی دارید؟"
            maxLength={500}
            value={note}
            onChange={(event) => setNote(event.target.value)}
          />
        </Card>
      </div>

      {/* Order summary */}
      <Card className="flex flex-col gap-md p-lg lg:sticky lg:top-24">
        <h2 className="font-headline text-display-md text-primary">خلاصه سفارش</h2>

        <dl className="flex flex-col gap-sm text-body-md">
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">جمع کالاها</dt>
            <dd className="tabular text-on-surface">{formatPrice(cart.subtotal)}</dd>
          </div>
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">تخفیف</dt>
            <dd className="tabular text-secondary">−{formatPrice(cart.discount)}</dd>
          </div>
          <div className="flex items-center justify-between">
            <dt className="text-on-surface-variant">هزینه ارسال</dt>
            <dd className="tabular text-on-surface">{formatPrice(shippingPrice)}</dd>
          </div>
          <div className="mt-sm flex items-center justify-between border-t border-paper-border pt-md">
            <dt className="text-body-lg font-label-md text-primary">مبلغ قابل پرداخت</dt>
            <dd className="tabular text-body-lg font-label-md text-primary">{formatPrice(total)}</dd>
          </div>
        </dl>

        {(error || empty) && (
          <p
            role="alert"
            className="flex items-start gap-xs rounded-lg bg-error-container px-md py-sm text-body-md text-on-error-container"
          >
            <Icon name="error" size={20} className="mt-px shrink-0" />
            {error ?? 'سبد خرید شما خالی است.'}
          </p>
        )}

        <Button size="lg" fullWidth loading={submitting} disabled={!canSubmit} onClick={submit}>
          ثبت سفارش و پرداخت
        </Button>

        <p className="flex items-start gap-xs text-caption leading-relaxed text-on-surface-variant">
          <Icon name="lock" size={16} className="mt-px shrink-0" />
          پرداخت از طریق درگاه امن بانکی انجام می‌شود. با ثبت سفارش، قوانین و مقررات بوژان را
          می‌پذیرید.
        </p>
      </Card>
    </div>
  );
}
