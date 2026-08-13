import { NextResponse } from 'next/server';
import { getAdminSession } from '@/lib/auth/server';

/**
 * The «لاگ سرور» screen's download links — one file, or all of them as a zip.
 *
 * Its own route for the same reason the backup download has one: the bytes come
 * from the API rather than from a public URL, and a plain link click cannot
 * send `X-Admin-User` itself. So the credential is attached here and the
 * response is streamed straight through rather than buffered — the archive of a
 * fortnight's logs is not something to hold in memory on the way past.
 *
 * `?name=` picks one file; omitting it asks for the lot.
 */
export async function GET(request: Request) {
  const session = await getAdminSession();
  if (!session || session.role !== 'owner') {
    return NextResponse.json({ error: 'دسترسی ندارید.' }, { status: 401 });
  }

  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  // No second `/admin` — the base already ends in one. See the test in
  // `lib/admin-proxy-paths.test.ts`, which exists because three routes got this
  // wrong and each reported its 404 as an ordinary failure.
  const root = base.replace(/\/$/, '');
  const name = new URL(request.url).searchParams.get('name');

  // The name is forwarded rather than trusted: the API resolves it through the
  // one choke point that decides which files may be read, and answers 404 to
  // anything that tries to leave the log directory.
  const upstream = await fetch(
    name
      ? `${root}/logs/${encodeURIComponent(name)}/download`
      : `${root}/logs/download-all`,
    {
      headers: {
        'X-Admin-User': session.sub,
        'X-Admin-Stamp': session.stamp,
        ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
      },
      cache: 'no-store',
    },
  );

  if (!upstream.ok || !upstream.body) {
    return NextResponse.json({ error: 'فایلی برای دانلود نیست.' }, { status: 404 });
  }

  return new NextResponse(upstream.body, {
    headers: {
      'Content-Type': upstream.headers.get('content-type') ?? 'application/octet-stream',
      'Content-Disposition':
        upstream.headers.get('content-disposition') ??
        `attachment; filename="${name ?? 'bojan-logs.zip'}"`,
    },
  });
}
