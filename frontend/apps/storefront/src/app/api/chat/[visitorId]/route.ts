import { NextResponse } from 'next/server';
import { api, useMockData } from '@/lib/api/client';

/**
 * The chat widget's read side.
 *
 * A thin proxy, not a session check: the visitor id in the path is the
 * widget's own opaque identifier (minted client-side, kept in
 * `localStorage`), not a customer — the same shape as the public support
 * contact form. Proxied so the API key stays server-side and the browser
 * never needs a cross-origin call.
 */
export async function GET(
  _request: Request,
  { params }: { params: Promise<{ visitorId: string }> },
) {
  const { visitorId } = await params;

  if (useMockData) return NextResponse.json([]);

  try {
    const messages = await api.get(`/chat/${visitorId}`);
    return NextResponse.json(messages);
  } catch {
    return NextResponse.json([], { status: 200 });
  }
}
