import { NextResponse } from 'next/server';
import { validateCoupon } from '@/lib/api/cart';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

const MAX_LINES = 50;
const MAX_QUANTITY = 20;

interface IncomingLine {
  productId: unknown;
  quantity: unknown;
}

function parseLines(value: unknown): Array<{ productId: string; quantity: number }> {
  if (!Array.isArray(value) || value.length === 0 || value.length > MAX_LINES) return [];

  const lines: Array<{ productId: string; quantity: number }> = [];

  for (const entry of value as IncomingLine[]) {
    const productId = typeof entry?.productId === 'string' ? entry.productId : '';
    const quantity = typeof entry?.quantity === 'number' ? entry.quantity : NaN;

    if (!productId || productId.length > 64) return [];
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > MAX_QUANTITY) return [];

    lines.push({ productId, quantity });
  }

  return lines;
}

/**
 * Screen 76 — check a discount code.
 *
 * Server-side because the discount has to be decided somewhere the shopper
 * cannot edit, and because guessing codes needs a limit: a short code space is
 * trivially walkable from the browser otherwise.
 */
export async function POST(request: Request) {
  const limit = rateLimit(clientKey(request, 'coupon'), 12, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'تلاش‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const body = (await request.json().catch(() => null)) as {
    code?: unknown;
    subtotal?: unknown;
    lines?: unknown;
  } | null;

  const code = typeof body?.code === 'string' ? body.code.trim() : '';
  const subtotal = typeof body?.subtotal === 'number' ? body.subtotal : 0;
  const lines = parseLines(body?.lines);

  if (code.length === 0 || code.length > 32) {
    return NextResponse.json({ error: 'کد تخفیف وارد شده معتبر نیست.' }, { status: 400 });
  }

  if (!Number.isFinite(subtotal) || subtotal <= 0) {
    return NextResponse.json({ error: 'سبد خرید شما خالی است.' }, { status: 400 });
  }

  try {
    const result = await validateCoupon(code, subtotal, lines);
    return NextResponse.json(result);
  } catch (cause) {
    return NextResponse.json(
      { error: cause instanceof Error ? cause.message : 'کد تخفیف معتبر نیست.' },
      { status: 400 },
    );
  }
}
