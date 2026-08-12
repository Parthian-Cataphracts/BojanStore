import { NextResponse } from 'next/server';
import { getAdminSession } from '@/lib/auth/server';

/**
 * Screen 156's download link.
 *
 * The backend streams the archive's bytes directly rather than redirecting
 * to a location — it is never at a public URL (see `IBackupArchiver`) — so
 * this route attaches the operator's credential and streams the response
 * straight through. A plain link click cannot send `X-Admin-User` itself.
 */
export async function GET(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const session = await getAdminSession();
  if (!session || session.role !== 'owner') {
    return NextResponse.json({ error: 'دسترسی ندارید.' }, { status: 401 });
  }

  const { id } = await params;
  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  // The base already ends in `/admin`. Repeating it here asked for
  // `/api/admin/admin/backups/...`, and the 404 that came back was reported as
  // "این نسخه هنوز فایلی ندارد" — so a backup that had run perfectly well
  // looked like one that had produced nothing.
  const upstream = await fetch(`${base.replace(/\/$/, '')}/backups/${id}/download`, {
    headers: {
      'X-Admin-User': session.sub,
      'X-Admin-Stamp': session.stamp,
      ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
    },
  });

  if (!upstream.ok || !upstream.body) {
    return NextResponse.json({ error: 'این نسخه هنوز فایلی ندارد.' }, { status: 404 });
  }

  return new NextResponse(upstream.body, {
    headers: {
      'Content-Type': upstream.headers.get('content-type') ?? 'application/octet-stream',
      'Content-Disposition': upstream.headers.get('content-disposition') ?? 'attachment',
    },
  });
}
