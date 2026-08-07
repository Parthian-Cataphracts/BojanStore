import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';
import { api, useMockData } from '@/lib/api/client';
import { clientKey, rateLimit } from '@/lib/auth/rate-limit';
import { VISITOR_COOKIE, verifyVisitorId } from '@/lib/chat/visitor';

/**
 * The chat widget's read side.
 *
 * There is no visitor id in the path any more. It comes from the signed
 * http-only cookie, so this route can only ever return the caller's own
 * conversation — where the old `/api/chat/[visitorId]` returned whichever
 * conversation the caller named.
 *
 * Still not a session check: a visitor may chat before signing in, the same way
 * the contact form on screen 47 accepts an anonymous sender. It is a check that
 * the id was issued by this server rather than typed in.
 */
export async function GET(request: Request) {
  // The widget polls every few seconds while open, so the ceiling is well above
  // that — it is here to stop a script from turning the poll into a flood, not
  // to pace the widget.
  const limit = rateLimit(clientKey(request, 'chat-read'), 120, 60);
  if (!limit.allowed) {
    return NextResponse.json([], { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } });
  }

  const store = await cookies();
  const visitorId = await verifyVisitorId(store.get(VISITOR_COOKIE)?.value);

  // No cookie means no conversation yet, which is an empty thread rather than
  // an error: this is what the widget shows the first time it is opened.
  if (!visitorId || useMockData) return NextResponse.json([]);

  try {
    return NextResponse.json(await api.get(`/chat/${visitorId}`));
  } catch {
    return NextResponse.json([], { status: 200 });
  }
}
