import { NextResponse } from 'next/server';
import { validateCoupon } from '@/lib/api/cart';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

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
  } | null;

  const code = typeof body?.code === 'string' ? body.code.trim() : '';
  const subtotal = typeof body?.subtotal === 'number' ? body.subtotal : 0;

  if (code.length === 0 || code.length > 32) {
    return NextResponse.json({ error: 'کد تخفیف وارد شده معتبر نیست.' }, { status: 400 });
  }

  if (!Number.isFinite(subtotal) || subtotal <= 0) {
    return NextResponse.json({ error: 'سبد خرید شما خالی است.' }, { status: 400 });
  }

  try {
    const result = await validateCoupon(code, subtotal);
    return NextResponse.json(result);
  } catch (cause) {
    return NextResponse.json(
      { error: cause instanceof Error ? cause.message : 'کد تخفیف معتبر نیست.' },
      { status: 400 },
    );
  }
}
