import { NextResponse } from 'next/server';
import { getAdminSession } from '@/lib/auth/server';
import { useMockData } from '@/lib/api/mock-data';

/**
 * What the dashboard's gauges poll.
 *
 * The card is server-rendered once with the page and then keeps itself current
 * from here, which is why this exists at all: the browser cannot call the API
 * directly — it holds neither `API_KEY` nor the operator's stamp — and the
 * alternative, `router.refresh()` on a timer, re-runs the whole dashboard and
 * its other two API calls every few seconds to update four numbers.
 *
 * Any signed-in operator, matching `GET /system/status` upstream, which is
 * `admin` rather than `admin:owner`. Nothing here is a secret: uptime, load and
 * free disk are what the card already showed everyone who could open it.
 */
export async function GET() {
  const session = await getAdminSession();
  if (!session) {
    return NextResponse.json({ error: 'دسترسی ندارید.' }, { status: 401 });
  }

  /*
    No fixture, deliberately. `getServerStatus` returns null on the mock path
    too, so the card is not rendered and nothing ever polls this — and a made-up
    load average is the one kind of mock that could be believed: every other
    fixture in the panel is obviously demo data, while "CPU 43%" is not.
  */
  if (useMockData) {
    return NextResponse.json({ error: 'وضعیت سرور در حالت نمونه در دسترس نیست.' }, { status: 404 });
  }

  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  // No second `/admin` — the base already ends in one. See
  // `lib/admin-proxy-paths.test.ts`.
  const root = base.replace(/\/$/, '');

  try {
    const upstream = await fetch(`${root}/system/status`, {
      headers: {
        'X-Admin-User': session.sub,
        'X-Admin-Stamp': session.stamp,
        ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
      },
      cache: 'no-store',
    });

    if (!upstream.ok) {
      return NextResponse.json({ error: 'وضعیت سرور در دسترس نیست.' }, { status: 502 });
    }

    return NextResponse.json(await upstream.json(), {
      headers: { 'Cache-Control': 'no-store' },
    });
  } catch {
    // A poll that cannot reach the API is not an error worth logging every few
    // seconds; the card keeps its last reading and says it is stale.
    return NextResponse.json({ error: 'وضعیت سرور در دسترس نیست.' }, { status: 502 });
  }
}
