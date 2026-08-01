import { NextResponse } from 'next/server';
import { placeOrder, type PlaceOrderInput } from '@/lib/api/cart';
import { getAddresses } from '@/lib/api/account';
import { paymentMethods, shippingMethods } from '@/lib/mock/checkout';
import { getSession } from '@/lib/auth/server';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

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
}

function parseLines(value: unknown): Array<{ productId: string; quantity: number }> | null {
  if (!Array.isArray(value) || value.length === 0 || value.length > MAX_LINES) return null;

  const lines: Array<{ productId: string; quantity: number }> = [];

  for (const entry of value as IncomingLine[]) {
    const productId = typeof entry?.productId === 'string' ? entry.productId : '';
    const quantity = typeof entry?.quantity === 'number' ? entry.quantity : NaN;

    if (!productId || productId.length > 64) return null;
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > MAX_QUANTITY) return null;

    lines.push({ productId, quantity });
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

  const input: PlaceOrderInput = {
    lines,
    addressId,
    shippingMethodId,
    paymentMethodId,
    ...(couponCode ? { couponCode } : null),
    ...(note ? { note } : null),
  };

  try {
    return NextResponse.json(await placeOrder(input));
  } catch (cause) {
    return NextResponse.json(
      { error: cause instanceof Error ? cause.message : 'ثبت سفارش انجام نشد.' },
      { status: 400 },
    );
  }
}
