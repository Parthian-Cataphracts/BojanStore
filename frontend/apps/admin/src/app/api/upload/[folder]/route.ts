import { NextResponse } from 'next/server';
import { getAdminSession } from '@/lib/auth/server';
import { rateLimit } from '@/lib/auth/rate-limit';

/**
 * Operator file uploads — product, brand, collection, content and campaign
 * imagery.
 *
 * Forwards to the API's own operator upload endpoint rather than writing
 * anything: the type is sniffed from the file's magic bytes there, the size is
 * capped there, and the stored name is generated there. What this layer owns is
 * the session, the folder allow-list and a per-operator rate limit.
 *
 * The API re-checks the role behind `X-Admin-User` against its own records, so
 * the check below is the first of two rather than the only one.
 */

const useMockData = process.env.NEXT_PUBLIC_USE_MOCK_DATA !== 'false';

/** Mirrors `AdminFolders` in the API's `UploadEndpoints`. */
const FOLDERS = new Set(['products', 'brands', 'collections', 'content', 'campaigns']);

/** The roles the API's own `admin:catalogue` policy admits. */
const CATALOGUE_ROLES = new Set(['owner', 'product']);

/** Matches the API's ceiling, so a file it would refuse is refused here first. */
const MAX_BYTES = 8 * 1024 * 1024;

export async function POST(
  request: Request,
  { params }: { params: Promise<{ folder: string }> },
) {
  const session = await getAdminSession();
  if (!session) {
    return NextResponse.json({ error: 'دسترسی ندارید.' }, { status: 401 });
  }

  if (!CATALOGUE_ROLES.has(session.role)) {
    return NextResponse.json(
      { error: 'برای این عملیات دسترسی لازم را ندارید.' },
      { status: 403 },
    );
  }

  const { folder } = await params;
  if (!FOLDERS.has(folder)) {
    return NextResponse.json({ error: 'این مقصد وجود ندارد.' }, { status: 404 });
  }

  const limit = rateLimit(`admin-upload:${session.sub}`, 30, 60);
  if (!limit.allowed) {
    return NextResponse.json(
      { error: 'درخواست‌های بیش از حد. کمی بعد دوباره تلاش کنید.' },
      { status: 429, headers: { 'Retry-After': String(limit.retryAfter) } },
    );
  }

  let file: File | null = null;
  try {
    const value = (await request.formData()).get('file');
    if (value instanceof File) file = value;
  } catch {
    return NextResponse.json({ error: 'فایل ارسالی خوانده نشد.' }, { status: 400 });
  }

  if (!file || file.size === 0) {
    return NextResponse.json({ error: 'فایلی انتخاب نشده است.' }, { status: 400 });
  }

  if (file.size > MAX_BYTES) {
    return NextResponse.json({ error: 'حجم فایل باید کمتر از ۸ مگابایت باشد.' }, { status: 413 });
  }

  if (useMockData) {
    // Nothing to forward to. A data URL previews locally and is exactly the
    // kind of value the API refuses to store, so it cannot be saved by mistake.
    const base64 = Buffer.from(await file.arrayBuffer()).toString('base64');
    return NextResponse.json({ url: `data:${file.type};base64,${base64}`, mock: true });
  }

  const base = process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) {
    return NextResponse.json({ error: 'آدرس سرور پیکربندی نشده است.' }, { status: 500 });
  }

  // Rebuilt rather than streamed through, so only the file field travels on.
  const upstream = new FormData();
  upstream.append('file', file, file.name);

  try {
    const response = await fetch(`${base.replace(/\/$/, '')}/admin/uploads/${folder}`, {
      method: 'POST',
      headers: {
        'X-Admin-User': session.sub,
        ...(process.env.API_KEY ? { 'X-Api-Key': process.env.API_KEY } : null),
      },
      body: upstream,
      cache: 'no-store',
    });

    if (!response.ok) {
      return NextResponse.json(
        { error: 'این فایل پذیرفته نشد. تصویری با فرمت JPG، PNG یا WebP انتخاب کنید.' },
        { status: response.status },
      );
    }

    return NextResponse.json((await response.json()) as unknown);
  } catch {
    return NextResponse.json(
      { error: 'ارتباط با سرور برقرار نشد. کمی بعد دوباره تلاش کنید.' },
      { status: 502 },
    );
  }
}
