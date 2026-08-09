import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';
import { api, useMockData } from '@/lib/api/client';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import { VISITOR_COOKIE, verifyVisitorId } from '@/lib/chat/visitor';

/**
 * "I am looking at the conversation right now."
 *
 * Split from the `GET` next door because the widget polls that one while the
 * panel is *closed*, to keep the launcher's unread badge current — and the
 * fetch used to mark every operator reply read, so the poll cleared the badge
 * it existed to raise. Reading is an act of the visitor's, not of the poll's,
 * so it gets its own call and the widget makes it only while the panel is open.
 */
export async function POST(request: Request) {
  const limit = rateLimit(clientKey(request, 'chat-read'), 120, 60);
  if (!limit.allowed) {
    return new NextResponse(null, {
      status: 429,
      headers: { 'Retry-After': String(limit.retryAfter) },
    });
  }

  const store = await cookies();
  const visitorId = await verifyVisitorId(store.get(VISITOR_COOKIE)?.value);

  // Nothing said yet means nothing to mark — not an error, just a no-op.
  if (!visitorId || useMockData) return new NextResponse(null, { status: 204 });

  try {
    await api.post(`/chat/${visitorId}/read`, {});
  } catch {
    // A missed read marker is corrected by the next one; it is never worth
    // showing the shopper an error over.
  }

  return new NextResponse(null, { status: 204 });
}
