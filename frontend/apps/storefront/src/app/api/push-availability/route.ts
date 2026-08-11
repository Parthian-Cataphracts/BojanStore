import { NextResponse } from 'next/server';
import { api, useMockData } from '@/lib/api/client';

/**
 * Whether the shop can send browser notifications, and the key to subscribe
 * against.
 *
 * Its own route rather than a field on some page's props: the answer changes the
 * moment the owner switches push on in the panel, and the toggle asks for it
 * again every time it is about to subscribe. Public, because the key is
 * published material — a browser needs it before it will agree to anything.
 *
 * Never cached. A shop that has just generated keys should work on the next
 * visit rather than after a revalidation window.
 */
export const dynamic = 'force-dynamic';

interface Availability {
  enabled: boolean;
  publicKey: string;
}

const off: Availability = { enabled: false, publicKey: '' };

export async function GET() {
  // Off in mock mode. There is no key to hand out and no server to register
  // with, and a toggle that appears to work while nothing is stored is worse
  // than one that stays hidden.
  if (useMockData) {
    return NextResponse.json(off, { headers: { 'Cache-Control': 'no-store' } });
  }

  // A backend that cannot be reached reads as "not available", which is the
  // safe answer: it hides the control rather than offering one that fails when
  // pressed.
  const availability = await api
    .get<Availability>('/push/availability')
    .catch(() => off);

  return NextResponse.json(availability, { headers: { 'Cache-Control': 'no-store' } });
}
