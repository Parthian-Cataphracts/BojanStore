import { NextResponse } from 'next/server';
import { getAdminSession } from '@/lib/auth/server';

/**
 * Screen 156's download link.
 *
 * The backend answers with a redirect to wherever the archive lives; this
 * route exists only to attach the operator's credential before forwarding to
 * it — the browser has no way to send `X-Admin-User` on a plain link click.
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

  const upstream = await fetch(`${base.replace(/\/$/, '')}/admin/backups/${id}/download`, {
    headers: {
      'X-Admin-User': session.sub,
      ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
    },
    redirect: 'manual',
  });

  const location = upstream.headers.get('location');
  if (upstream.status >= 300 && upstream.status < 400 && location) {
    return NextResponse.redirect(location);
  }

  return NextResponse.json({ error: 'این نسخه هنوز فایلی ندارد.' }, { status: 404 });
}
