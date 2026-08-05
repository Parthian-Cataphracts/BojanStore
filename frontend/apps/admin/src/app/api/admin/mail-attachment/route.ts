import { NextResponse } from 'next/server';
import { getAdminSession } from '@/lib/auth/server';
import { rateLimit } from '@/lib/auth/rate-limit';
import { useMockData } from '@/lib/api/mock-data';

/**
 * Streams one inbound mail attachment back to the operator.
 *
 * A route of its own rather than an entry in the `[resource]` write proxy: that
 * one posts JSON and reads JSON, and this returns bytes.
 *
 * Everything about the file is chosen by whoever sent the mail, so nothing
 * about it is honoured. The upstream serves it as `application/octet-stream`
 * with `Content-Disposition: attachment`, and both are passed through
 * unchanged — an HTML or SVG attachment rendered inline on the panel's own
 * origin would be script execution with the operator's session, which is
 * exactly what the body sanitizer exists to prevent one layer up.
 */
export const dynamic = 'force-dynamic';

/** The roles the API's own support policy admits. */
const ALLOWED_ROLES = new Set(['owner', 'support']);

export async function GET(request: Request) {
  const session = await getAdminSession();
  if (!session) {
    return NextResponse.json({ error: 'دسترسی ندارید.' }, { status: 401 });
  }

  if (!ALLOWED_ROLES.has(session.role)) {
    return NextResponse.json({ error: 'برای این عملیات دسترسی لازم را ندارید.' }, { status: 403 });
  }

  const limit = rateLimit(`mail-attachment:${session.sub}`, 60, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  const url = new URL(request.url);
  const folder = url.searchParams.get('folder') ?? '';
  const uid = Number(url.searchParams.get('uid'));
  const index = Number(url.searchParams.get('index'));

  // Checked here so a malformed link is a 400 rather than a round trip that
  // ends in one.
  if (!folder || !Number.isInteger(uid) || uid < 0 || !Number.isInteger(index) || index < 0) {
    return NextResponse.json({ error: 'پیوست نامعتبر است.' }, { status: 400 });
  }

  if (useMockData) {
    return NextResponse.json({ error: 'پیوست در این حالت در دسترس نیست.' }, { status: 501 });
  }

  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  const upstream = await fetch(
    `${base.replace(/\/$/, '')}/support/mailbox/attachments/${encodeURIComponent(folder)}/${uid}/${index}`,
    {
      headers: {
        'X-Admin-User': session.sub,
        ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
      },
      cache: 'no-store',
    },
  );

  if (!upstream.ok) {
    return NextResponse.json({ error: 'دریافت پیوست ممکن نشد.' }, { status: upstream.status });
  }

  return new NextResponse(upstream.body, {
    status: 200,
    headers: {
      'Content-Type': 'application/octet-stream',
      'Content-Disposition':
        upstream.headers.get('content-disposition') ?? 'attachment; filename="attachment"',
      // Belt and braces against a browser deciding for itself that an
      // octet-stream is really HTML.
      'X-Content-Type-Options': 'nosniff',
      'Cache-Control': 'no-store',
    },
  });
}
