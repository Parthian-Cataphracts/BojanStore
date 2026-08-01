import { NextResponse } from 'next/server';
import { normalizeDigitsInput } from '@bojan/ui';
import { trackOrder } from '@/lib/api/cart';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';

/**
 * Screen 30 — guest order tracking.
 *
 * Public and unauthenticated, so it is the obvious target for enumerating
 * order numbers; rate-limited per client on top of whatever the .NET backend
 * itself enforces, and it requires both the number and the phone the order was
 * placed with — see `trackOrder`.
 */
export async function GET(request: Request) {
  const limit = rateLimit(clientKey(request, 'orders-track'), 10, 300);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'تلاش‌های زیاد. چند دقیقه بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const url = new URL(request.url);
  const number = (url.searchParams.get('number') ?? '').trim();
  const phone = normalizeDigitsInput(url.searchParams.get('phone') ?? '');

  if (!number || number.length > 32 || !/^09\d{9}$/.test(phone)) {
    return NextResponse.json({ error: 'شماره سفارش یا موبایل نامعتبر است.' }, { status: 400 });
  }

  const order = await trackOrder(number, phone);
  if (!order) return NextResponse.json({ error: 'سفارشی پیدا نشد.' }, { status: 404 });

  return NextResponse.json(order);
}
