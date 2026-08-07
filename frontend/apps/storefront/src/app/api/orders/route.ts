import { NextResponse } from 'next/server';
import {
  getPaymentMethods,
  getShippingMethods,
  placeOrder,
  type PlaceOrderInput,
} from '@/lib/api/cart';
import { getAddresses } from '@/lib/api/account';
import { getSession } from '@/lib/auth/server';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import { problemMessage } from '@/lib/api/problem';
import { ApiError } from '@/lib/api/client';

/**
 * Screens 08 and 77-78 — place the order.
 *
 * Everything the browser sends is re-checked here. The basket lives in the
 * shopper's own storage, so treating it as trustworthy would let anyone post a
 * quantity of −1 or an address belonging to someone else; the address is
 * matched against the signed-in customer's own list, and the shipping and
 * payment ids against the catalogue of methods rather than against whatever
 * arrived in the body.
 */

const MAX_LINES = 50;
const MAX_QUANTITY = 20;
const MAX_NOTE = 500;

interface IncomingLine {
  productId: unknown;
  quantity: unknown;
  skuId?: unknown;
}

function parseLines(value: unknown): Array<{ productId: string; quantity: number; skuId?: string }> | null {
  if (!Array.isArray(value) || value.length === 0 || value.length > MAX_LINES) return null;

  const lines: Array<{ productId: string; quantity: number; skuId?: string }> = [];

  for (const entry of value as IncomingLine[]) {
    const productId = typeof entry?.productId === 'string' ? entry.productId : '';
    const quantity = typeof entry?.quantity === 'number' ? entry.quantity : NaN;
    const skuId = typeof entry?.skuId === 'string' && entry.skuId.length <= 64 ? entry.skuId : undefined;

    if (!productId || productId.length > 64) return null;
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > MAX_QUANTITY) return null;

    lines.push({ productId, quantity, ...(skuId ? { skuId } : null) });
  }

  return lines;
}

export async function POST(request: Request) {
  const session = await getSession();
  if (!session) {
    return NextResponse.json({ error: 'برای ثبت سفارش وارد حساب خود شوید.' }, { status: 401 });
  }

  const limit = rateLimit(`order:${session.sub}`, 10, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'تلاش‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }
  // Second bucket keyed by address, so one account cannot be used as a relay.
  if (!rateLimit(clientKey(request, 'order'), 30, 60).allowed) {
    return NextResponse.json({ error: 'تلاش‌های بیش از حد.' }, { status: 429 });
  }

  const body = (await request.json().catch(() => null)) as Record<string, unknown> | null;

  const lines = parseLines(body?.lines);
  if (!lines) {
    return NextResponse.json({ error: 'سبد خرید شما معتبر نیست.' }, { status: 400 });
  }

  const addressId = typeof body?.addressId === 'string' ? body.addressId : '';
  const addresses = await getAddresses();
  if (!addresses.some((address) => address.id === addressId)) {
    return NextResponse.json({ error: 'آدرس تحویل را انتخاب کنید.' }, { status: 400 });
  }

  // Checked against the methods the shop offers, not the fixture. Validating
  // against the fixture meant a method the panel added was refused here before
  // the API ever saw it, with "روش ارسال را انتخاب کنید" on a method the
  // shopper had just been shown.
  const [shippingMethods, paymentMethods] = await Promise.all([
    getShippingMethods(),
    getPaymentMethods(),
  ]);

  const shippingMethodId = typeof body?.shippingMethodId === 'string' ? body.shippingMethodId : '';
  if (!shippingMethods.some((method) => method.id === shippingMethodId)) {
    return NextResponse.json({ error: 'روش ارسال را انتخاب کنید.' }, { status: 400 });
  }

  const paymentMethodId = typeof body?.paymentMethodId === 'string' ? body.paymentMethodId : '';
  if (!paymentMethods.some((method) => method.id === paymentMethodId)) {
    return NextResponse.json({ error: 'روش پرداخت را انتخاب کنید.' }, { status: 400 });
  }

  const note = typeof body?.note === 'string' ? body.note.slice(0, MAX_NOTE) : undefined;
  const couponCode =
    typeof body?.couponCode === 'string' && body.couponCode.length <= 32
      ? body.couponCode
      : undefined;

  // Screen 74's chosen window, carried through so the order records what the
  // shopper asked for. Bounded to the API's own column, and it is only ever
  // displayed, never parsed.
  const deliveryWindow =
    typeof body?.deliveryWindow === 'string' && body.deliveryWindow.length <= 200
      ? body.deliveryWindow
      : undefined;

  const input: PlaceOrderInput = {
    lines,
    addressId,
    shippingMethodId,
    paymentMethodId,
    ...(couponCode ? { couponCode } : null),
    ...(note ? { note } : null),
    ...(deliveryWindow ? { deliveryWindow } : null),
  };

  // Passed through from the browser, which mints one per checkout attempt. It
  // is bounded and stripped of anything but the characters a key needs, because
  // it reaches the API as a header and is stored against the order. Absent, the
  // API derives one from the basket — correct for a double-tap, but it has no
  // sense of time, so a repeat purchase of the same basket would be answered
  // with the original order instead of placing a new one.
  const submittedKey = request.headers.get('Idempotency-Key') ?? '';
  const idempotencyKey = /^[A-Za-z0-9._-]{1,200}$/.test(submittedKey) ? submittedKey : undefined;

  try {
    return NextResponse.json(await placeOrder(input, idempotencyKey));
  } catch (cause) {
    // `ApiError`'s message names the upstream path and status ("درخواست /orders
    // با خطای 500…") — neither useful to a shopper nor something to hand a
    // browser. Every refusal a shopper can act on is already answered as a
    // field error above. Anything else the mock path raises is written for the
    // shopper and is passed through.
    if (cause instanceof ApiError) {
      // The API's own reason, when it gave one. Every refusal used to collapse
      // into "کمی بعد دوباره تلاش کنید", which for a sold-out product or a
      // spent coupon tells the shopper to do the one thing that cannot help.
      return NextResponse.json(
        { error: problemMessage(cause) ?? 'ثبت سفارش انجام نشد. کمی بعد دوباره تلاش کنید.' },
        { status: cause.status === 409 ? 409 : 400 },
      );
    }

    return NextResponse.json(
      { error: cause instanceof Error ? cause.message : 'ثبت سفارش انجام نشد.' },
      { status: 400 },
    );
  }
}
