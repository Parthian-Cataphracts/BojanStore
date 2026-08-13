import { NextResponse } from 'next/server';
import { getAdminSession } from '@/lib/auth/server';

/**
 * Screen 140's download link.
 *
 * `GET /admin/reports/export/{id}/download` has existed since exports were
 * written and nothing in the panel ever called it. The screen said instead that
 * a link would be emailed — and no part of the export worker sends mail, so on
 * a shop with no mailbox configured (and on one with a mailbox, come to that)
 * the file was built, stored, and reachable only by somebody who knew the API's
 * address and could sign a request to it. An operator pressing «ساخت خروجی» was
 * told to wait for an email that was never going to arrive.
 *
 * Same shape as the backup download beside it: the API streams the bytes and
 * never puts the file at a public URL, so the credential has to be attached
 * here — a link click cannot send `X-Admin-User` on its own.
 */
export async function GET(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const session = await getAdminSession();
  if (!session) {
    return NextResponse.json({ error: 'دسترسی ندارید.' }, { status: 401 });
  }

  const { id } = await params;
  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  // The base already ends in `/admin`; the export route is the one under it.
  const upstream = await fetch(`${base.replace(/\/$/, '')}/reports/export/${id}/download`, {
    headers: {
      'X-Admin-User': session.sub,
      'X-Admin-Stamp': session.stamp,
      ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
    },
    cache: 'no-store',
  });

  if (!upstream.ok || !upstream.body) {
    // The API answers 404 both for "no such export" and for "queued, not
    // finished". From here they are the same thing — there is no file yet —
    // and the wait is seconds, so the sentence says to try again rather than
    // implying the export failed.
    return NextResponse.json(
      { error: 'این خروجی هنوز آماده نیست. چند لحظه بعد دوباره تلاش کنید.' },
      { status: 404 },
    );
  }

  return new NextResponse(upstream.body, {
    headers: {
      'Content-Type': upstream.headers.get('content-type') ?? 'application/octet-stream',
      'Content-Disposition': upstream.headers.get('content-disposition') ?? 'attachment',
    },
  });
}
